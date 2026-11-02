import { MessageFlags, type GatewayMessageCreateDispatchData } from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { addressesBot, replyToMention } from '../src/chat.ts';
import { commandTools } from '../src/commands/tools.ts';
import type { Env } from '../src/env.ts';

const BOT_ID = '100';
const token = `${btoa(BOT_ID)}.fake.token`;
const env = { OPENAI_API_KEY: 'test-key' } as Env;
const bot = { id: BOT_ID, username: 'devbot', global_name: 'DevBot' };
const kyle = { id: '200', username: 'kyle', global_name: 'Kyle' };
const sam = { id: '300', username: 'sam', global_name: null };

const mention = {
  id: '9',
  guild_id: '1',
  channel_id: '42',
  author: kyle,
  content: `<@${BOT_ID}> what should we play?`,
  mentions: [bot],
} as unknown as GatewayMessageCreateDispatchData;

const chooseArgs = {
  option1: 'WoW',
  option2: 'Overwatch',
  option3: null,
  option4: null,
  option5: null,
  option6: null,
  option7: null,
};

interface OpenAIRequest {
  input: { type?: string; call_id?: string; output?: string }[];
  tools: { type: string; name?: string }[];
  tool_choice: string;
}

const fetchMock = vi.fn<typeof fetch>();
let historyResponse: Response;
let openai: (request: OpenAIRequest) => Response;

function openaiText(text: string) {
  return Response.json({
    object: 'response',
    output: [{ type: 'message', role: 'assistant', content: [{ type: 'output_text', text }] }],
  });
}

function openaiCalls(...calls: [name: string, args: object][]) {
  return Response.json({
    object: 'response',
    output: calls.map(([name, args], i) => ({
      type: 'function_call',
      id: `fc_${i}`,
      call_id: `call_${i}`,
      name,
      arguments: JSON.stringify(args),
      status: 'completed',
    })),
  });
}

function calls(path: string) {
  return fetchMock.mock.calls.filter(([url]) => String(url).includes(path));
}

function openaiRequests(): OpenAIRequest[] {
  return calls('/v1/responses').map(([, init]) => JSON.parse(init!.body as string));
}

function sentReply() {
  const [post] = calls('/channels/42/messages').filter(([, init]) => init?.method === 'POST');
  return JSON.parse(post![1]!.body as string);
}

beforeEach(() => {
  fetchMock.mockReset();
  historyResponse = Response.json([]);
  openai = () => openaiText('Halo!');
  fetchMock.mockImplementation(async (url, init) => {
    const path = String(url);
    if (path.endsWith('/typing')) return new Response(null, { status: 204 });
    if (path.endsWith('/users/@me')) return Response.json(bot);
    if (path.startsWith('https://cdn.discordapp.com/')) {
      return new Response(new Uint8Array([137, 80, 78, 71]));
    }
    if (path.includes('/messages?')) return historyResponse;
    if (path.endsWith('/v1/responses')) return openai(JSON.parse(init!.body as string));
    if (path.endsWith('/v1/decisions')) {
      return Response.json({ answers: [{ type: 'choice', choice: 'yes' }] });
    }
    return Response.json({ id: 'reply' });
  });
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('addressesBot', () => {
  it.each([
    ['a person mentions the bot', '1', kyle, [bot], true],
    ['a person mentions someone else', '1', kyle, [sam], false],
    ['nobody is mentioned', '1', kyle, [], false],
    ['another bot mentions the bot', '1', { ...sam, bot: true }, [bot], false],
    ['a person DMs the bot', undefined, kyle, [], true],
    ['a bot DMs the bot', undefined, { ...sam, bot: true }, [], false],
  ])('%s → %s', (_, guild_id, author, mentions, expected) => {
    const message = { guild_id, author, mentions } as unknown as GatewayMessageCreateDispatchData;
    expect(addressesBot(message, token)).toBe(expected);
  });
});

describe('replyToMention', () => {
  it('sends recent history to OpenAI in order and replies to the mention', async () => {
    historyResponse = Response.json([
      { author: bot, content: 'Hi Sam!', mentions: [] },
      { author: sam, content: `hey <@${BOT_ID}> and <@${kyle.id}>`, mentions: [bot, kyle] },
      { author: kyle, content: '', mentions: [] },
    ]);

    await replyToMention(mention, token, env);

    expect(String(calls('/messages?')[0]![0])).toBe(
      'https://discord.com/api/v10/channels/42/messages?before=9&limit=49',
    );
    expect(calls('/channels/42/typing')).toHaveLength(1);
    const [request] = openaiRequests();
    expect(request).toMatchObject({
      model: 'gpt-6.1-terra',
      reasoning: { effort: 'none' },
      tool_choice: 'auto',
      input: [
        { role: 'user', content: 'sam: hey @DevBot and @Kyle' },
        { role: 'assistant', content: 'Hi Sam!' },
        { role: 'user', content: 'Kyle: @DevBot what should we play?' },
      ],
    });
    expect(request!.tools.map((tool) => tool.name ?? tool.type)).toEqual([
      ...commandTools.map((tool) => tool.name),
      'web_search',
      'code_interpreter',
    ]);
    expect((request as unknown as { instructions: string }).instructions).toContain(
      'You are DevBot, a bot chatting in a Discord channel',
    );
    expect(sentReply()).toEqual({
      content: 'Halo!',
      message_reference: { message_id: '9', fail_if_not_exists: false },
      allowed_mentions: { parse: [], replied_user: true },
      flags: MessageFlags.SuppressEmbeds,
    });
  });

  it('answers a DM that does not mention the bot with a plain message', async () => {
    const dm = { ...mention, guild_id: undefined, content: 'what should we play?', mentions: [] };

    await replyToMention(dm as unknown as GatewayMessageCreateDispatchData, token, env);

    const [request] = openaiRequests();
    expect(request!.input).toEqual([{ role: 'user', content: 'Kyle: what should we play?' }]);
    const { instructions } = request as unknown as { instructions: string };
    expect(instructions).toContain('You are DevBot, a bot chatting in a Discord DM');
    expect(instructions).not.toContain('server');
    expect(sentReply()).toEqual({
      content: 'Halo!',
      allowed_mentions: { parse: [], replied_user: true },
      flags: MessageFlags.SuppressEmbeds,
    });
  });

  it('still replies from the mention alone when history cannot be read', async () => {
    historyResponse = new Response('Missing Access', { status: 403 });

    await replyToMention(mention, token, env);

    expect(openaiRequests()[0]!.input).toEqual([
      { role: 'user', content: 'Kyle: @DevBot what should we play?' },
    ]);
    expect(sentReply().content).toBe('Halo!');
  });

  it('cuts replies down to Discord’s 2,000-character limit', async () => {
    openai = () => openaiText('a'.repeat(2_500));

    await replyToMention(mention, token, env);

    expect(sentReply().content).toBe(`${'a'.repeat(1_999)}…`);
  });

  it('runs every command the model calls and notes them under the reply', async () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.9);
    openai = (request) =>
      request.input.some((item) => item.type === 'function_call_output')
        ? openaiText('Go with Overwatch, and the conch agrees!')
        : openaiCalls(['choose', chooseArgs], ['conch', { question: 'Is Overwatch good?' }]);

    await replyToMention(mention, token, env);

    const requests = openaiRequests();
    expect(requests).toHaveLength(2);
    expect(requests[1]!.input.slice(-4)).toMatchObject([
      { type: 'function_call', call_id: 'call_0' },
      { type: 'function_call', call_id: 'call_1' },
      {
        type: 'function_call_output',
        call_id: 'call_0',
        output: 'Choosing between: WoW, Overwatch\nI choose **Overwatch**!',
      },
      { type: 'function_call_output', call_id: 'call_1', output: 'Yes' },
    ]);
    expect(calls('/channels/42/typing')).toHaveLength(2);
    expect(sentReply().content).toBe(
      'Go with Overwatch, and the conch agrees!\n-# used /choose, /conch',
    );
  });

  it('answers from a web search in one round', async () => {
    const search = {
      type: 'web_search_call',
      id: 'ws_0',
      status: 'completed',
      action: { type: 'search', query: 'overwatch patch notes' },
    };
    const answer = 'A new hero dropped ([blizzard.com](https://blizzard.com/news))';
    openai = () =>
      Response.json({
        object: 'response',
        output: [
          search,
          { type: 'message', role: 'assistant', content: [{ type: 'output_text', text: answer }] },
        ],
      });

    await replyToMention(mention, token, env);

    expect(openaiRequests()).toHaveLength(1);
    expect(sentReply().content).toBe(answer);
  });

  it('keeps hosted tool calls in context when the model also runs a command', async () => {
    const search = {
      type: 'web_search_call',
      id: 'ws_0',
      status: 'completed',
      action: { type: 'search', query: 'best game right now' },
    };
    openai = (request) => {
      if (request.input.some((item) => item.type === 'function_call_output')) {
        return openaiText('Overwatch it is');
      }
      return Response.json({
        object: 'response',
        output: [
          search,
          {
            type: 'function_call',
            id: 'fc_0',
            call_id: 'call_0',
            name: 'choose',
            arguments: JSON.stringify(chooseArgs),
            status: 'completed',
          },
        ],
      });
    };

    await replyToMention(mention, token, env);

    expect(openaiRequests()[1]!.input.slice(-3)).toMatchObject([
      search,
      { type: 'function_call', call_id: 'call_0' },
      { type: 'function_call_output', call_id: 'call_0' },
    ]);
    expect(sentReply().content).toBe('Overwatch it is\n-# used /choose');
  });

  it('makes the model answer after 3 rounds of commands', async () => {
    openai = (request) =>
      request.tool_choice === 'none'
        ? openaiText('Out of ideas')
        : openaiCalls(['choose', chooseArgs]);

    await replyToMention(mention, token, env);

    expect(openaiRequests().map((request) => request.tool_choice)).toEqual([
      'auto',
      'auto',
      'auto',
      'none',
    ]);
    expect(sentReply().content).toBe('Out of ideas\n-# used /choose');
  });

  it('keeps the command note when cutting a long reply down', async () => {
    openai = (request) =>
      request.input.some((item) => item.type === 'function_call_output')
        ? openaiText('a'.repeat(2_500))
        : openaiCalls(['choose', chooseArgs]);

    await replyToMention(mention, token, env);

    const { content } = sentReply();
    expect(content).toHaveLength(2_000);
    expect(content.endsWith('a…\n-# used /choose')).toBe(true);
  });

  describe('caption this', () => {
    const attachment = (id: string, contentType = 'image/png') => ({
      id,
      filename: `${id}.png`,
      content_type: contentType,
      size: 4,
      url: `https://cdn.discordapp.com/attachments/42/${id}/${id}.png`,
    });
    const captionThis = (extra: object = {}) =>
      ({
        ...mention,
        content: `<@${BOT_ID}> caption this`,
        ...extra,
      }) as unknown as GatewayMessageCreateDispatchData;
    const downloads = () => calls('https://cdn.discordapp.com/').map(([url]) => String(url));

    beforeEach(() => {
      openai = (request) => {
        if (JSON.stringify(request.input).includes('input_image')) {
          return openaiText('Me when the build passes');
        }
        return request.input.some((item) => item.type === 'function_call_output')
          ? openaiText('Me when the build passes')
          : openaiCalls(['caption', {}]);
      };
    });

    it.each([
      [
        'attached to the mention',
        {
          attachments: [attachment('own')],
          referenced_message: { attachments: [attachment('re')] },
        },
        'own',
      ],
      [
        'on the message it replies to',
        { attachments: [], referenced_message: { attachments: [attachment('re')] } },
        're',
      ],
      ['most recent in the channel', {}, 'new'],
    ])('captions the image %s', async (_, extra, id) => {
      historyResponse = Response.json([
        {
          author: sam,
          content: 'notes',
          mentions: [],
          attachments: [attachment('doc', 'text/plain')],
        },
        { author: sam, content: '', mentions: [], attachments: [attachment('new')] },
        { author: kyle, content: '', mentions: [], attachments: [attachment('old')] },
      ]);

      await replyToMention(captionThis(extra), token, env);

      expect(downloads()).toEqual([`https://cdn.discordapp.com/attachments/42/${id}/${id}.png`]);
      expect(openaiRequests()[2]!.input.at(-1)).toMatchObject({
        type: 'function_call_output',
        output: 'Me when the build passes',
      });
      expect(sentReply().content).toBe('Me when the build passes\n-# used /caption');
    });

    it('tells the model when there is no image to caption', async () => {
      await replyToMention(captionThis(), token, env);

      expect(downloads()).toEqual([]);
      expect(openaiRequests()[1]!.input.at(-1)).toMatchObject({
        type: 'function_call_output',
        output: 'Error: Upload a PNG, JPEG, WebP, or GIF image to caption.',
      });
    });
  });

  it('gives up and apologizes when the reply runs out of time', async () => {
    const deadline = new AbortController();
    const respond = fetchMock.getMockImplementation()!;
    fetchMock.mockImplementation((url, init) => {
      if (!String(url).endsWith('/v1/responses')) return respond(url, init);
      return new Promise((_resolve, reject) => {
        const signal = init!.signal!;
        if (signal.aborted) return reject(signal.reason);
        signal.addEventListener('abort', () => reject(signal.reason), { once: true });
      });
    });

    const reply = replyToMention(mention, token, env, deadline.signal);
    await vi.waitFor(() => expect(openaiRequests()).toHaveLength(1));
    deadline.abort();
    await reply;

    expect(sentReply().content).toBe(
      "Sorry, I couldn't come up with a reply. Try again in a moment.",
    );
  });

  it.each([
    ['OpenAI rejects the request', env, () => new Response('Unauthorized', { status: 401 })],
    ['OpenAI returns no text', env, () => openaiText('  ')],
    ['no API key is set', {} as Env, () => openaiText('Halo!')],
  ])('apologizes when %s', async (_, chatEnv, response) => {
    openai = response;

    await replyToMention(mention, token, chatEnv);

    expect(sentReply().content).toBe(
      "Sorry, I couldn't come up with a reply. Try again in a moment.",
    );
    expect(console.error).toHaveBeenCalled();
  });
});
