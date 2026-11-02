import type { GatewayMessageCreateDispatchData } from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { mentionsBot, replyToMention } from '../src/chat.ts';

const BOT_ID = '100';
const token = `${btoa(BOT_ID)}.fake.token`;
const bot = { id: BOT_ID, username: 'devbot', global_name: 'DevBot' };
const kyle = { id: '200', username: 'kyle', global_name: 'Kyle' };
const sam = { id: '300', username: 'sam', global_name: null };

const mention = {
  id: '9',
  channel_id: '42',
  author: kyle,
  content: `<@${BOT_ID}> what should we play?`,
  mentions: [bot],
} as unknown as GatewayMessageCreateDispatchData;

const fetchMock = vi.fn<typeof fetch>();
let historyResponse: Response;
let openaiResponse: Response;

function openaiText(text: string) {
  return Response.json({
    object: 'response',
    output: [{ type: 'message', role: 'assistant', content: [{ type: 'output_text', text }] }],
  });
}

function calls(path: string) {
  return fetchMock.mock.calls.filter(([url]) => String(url).includes(path));
}

function sentReply() {
  const [post] = calls('/channels/42/messages').filter(([, init]) => init?.method === 'POST');
  return JSON.parse(post![1]!.body as string);
}

beforeEach(() => {
  fetchMock.mockReset();
  historyResponse = Response.json([]);
  openaiResponse = openaiText('Halo!');
  fetchMock.mockImplementation(async (url) => {
    const path = String(url);
    if (path.endsWith('/typing')) return new Response(null, { status: 204 });
    if (path.includes('/messages?')) return historyResponse;
    if (path.endsWith('/v1/responses')) return openaiResponse;
    return Response.json({ id: 'reply' });
  });
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('mentionsBot', () => {
  it.each([
    ['a person mentions the bot', kyle, [bot], true],
    ['a person mentions someone else', kyle, [sam], false],
    ['nobody is mentioned', kyle, [], false],
    ['another bot mentions the bot', { ...sam, bot: true }, [bot], false],
  ])('%s → %s', (_, author, mentions, expected) => {
    const message = { author, mentions } as unknown as GatewayMessageCreateDispatchData;
    expect(mentionsBot(message, token)).toBe(expected);
  });
});

describe('replyToMention', () => {
  it('sends recent history to OpenAI in order and replies to the mention', async () => {
    historyResponse = Response.json([
      { author: bot, content: 'Hi Sam!', mentions: [] },
      { author: sam, content: `hey <@${BOT_ID}> and <@${kyle.id}>`, mentions: [bot, kyle] },
      { author: kyle, content: '', mentions: [] },
    ]);

    await replyToMention(mention, token, 'test-key');

    expect(String(calls('/messages?')[0]![0])).toBe(
      'https://discord.com/api/v10/channels/42/messages?before=9&limit=49',
    );
    expect(calls('/channels/42/typing')).toHaveLength(1);
    const request = JSON.parse(calls('/v1/responses')[0]![1]!.body as string);
    expect(request).toMatchObject({
      model: 'gpt-6.1-terra',
      reasoning: { effort: 'none' },
      input: [
        { role: 'user', content: 'sam: hey @DevBot and @Kyle' },
        { role: 'assistant', content: 'Hi Sam!' },
        { role: 'user', content: 'Kyle: @DevBot what should we play?' },
      ],
    });
    expect(request.instructions).toContain('You are DevBot');
    expect(sentReply()).toEqual({
      content: 'Halo!',
      message_reference: { message_id: '9', fail_if_not_exists: false },
      allowed_mentions: { parse: [], replied_user: true },
    });
  });

  it('still replies from the mention alone when history cannot be read', async () => {
    historyResponse = new Response('Missing Access', { status: 403 });

    await replyToMention(mention, token, 'test-key');

    const request = JSON.parse(calls('/v1/responses')[0]![1]!.body as string);
    expect(request.input).toEqual([
      { role: 'user', content: 'Kyle: @DevBot what should we play?' },
    ]);
    expect(sentReply().content).toBe('Halo!');
  });

  it('cuts replies down to Discord’s 2,000-character limit', async () => {
    openaiResponse = openaiText('a'.repeat(2_500));

    await replyToMention(mention, token, 'test-key');

    expect(sentReply().content).toBe(`${'a'.repeat(1_999)}…`);
  });

  it.each([
    ['OpenAI rejects the request', 'test-key', () => new Response('Unauthorized', { status: 401 })],
    ['OpenAI returns no text', 'test-key', () => openaiText('  ')],
    ['no API key is set', undefined, () => openaiText('Halo!')],
  ])('apologizes when %s', async (_, apiKey, openai) => {
    openaiResponse = openai();

    await replyToMention(mention, token, apiKey);

    expect(sentReply().content).toBe(
      "Sorry, I couldn't come up with a reply. Try again in a moment.",
    );
    expect(console.error).toHaveBeenCalled();
  });
});
