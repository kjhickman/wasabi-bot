import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIApplicationCommandInteractionDataOption,
  type APIAttachment,
  type APIChatInputApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { caption } from '../src/commands/caption.ts';
import type { Env } from '../src/env.ts';

const fetchMock = vi.fn<typeof fetch>();
const url = 'https://cdn.discordapp.com/ephemeral-attachments/1/700/cat.png';
const png = [137, 80, 78, 71];
const imageOption: APIApplicationCommandInteractionDataOption = {
  type: ApplicationCommandOptionType.Attachment,
  name: 'image',
  value: '700',
};
const attachment = { id: '700', filename: 'cat.png', content_type: 'image/png', size: 4, url };
const errorResponse = {
  type: InteractionResponseType.ChannelMessageWithSource,
  data: { content: "I couldn't think of a caption. Try again.", flags: MessageFlags.Ephemeral },
};

let download: () => Response;
let openai: (init: RequestInit) => Response | Promise<Response>;

function textResponse(text: string) {
  return Response.json({
    object: 'response',
    output: [{ type: 'message', role: 'assistant', content: [{ type: 'output_text', text }] }],
  });
}

function openaiCalls() {
  return fetchMock.mock.calls.filter(([input]) => String(input).startsWith('https://api.openai'));
}

function run(
  options: APIApplicationCommandInteractionDataOption[] | null = [imageOption],
  attachments: Record<string, Partial<APIAttachment>> | null = { '700': attachment },
  env = { OPENAI_API_KEY: 'test-key' } as Env,
  signal = new AbortController().signal,
) {
  const data = {
    id: '1',
    type: ApplicationCommandType.ChatInput,
    name: 'caption',
    ...(options && { options }),
    ...(attachments && { resolved: { attachments } }),
  } as APIChatInputApplicationCommandInteractionData;
  return caption.execute(data, env, signal);
}

beforeEach(() => {
  download = () => new Response(new Uint8Array(png), { headers: { 'Content-Type': 'image/png' } });
  openai = () => textResponse('Hey!');
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (input, init) =>
    String(input) === url ? download() : openai(init!),
  );
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('/caption', () => {
  it('defines a required image attachment and replies after deferring', () => {
    expect(caption.definition).toEqual({
      name: 'caption',
      description: expect.any(String),
      type: ApplicationCommandType.ChatInput,
      options: [
        {
          type: ApplicationCommandOptionType.Attachment,
          name: 'image',
          description: expect.any(String),
          required: true,
        },
      ],
    });
    expect(caption.deferred).toBe(true);
  });

  it('captions the downloaded image and re-posts it without pinging anyone', async () => {
    openai = () => textResponse('  When the code works on the first try  ');

    const response = await run();

    expect(response).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: {
        content: 'When the code works on the first try',
        allowed_mentions: { parse: [] },
      },
      files: [expect.any(File)],
    });
    const file = response.files![0]!;
    expect(file.name).toBe('cat.png');
    expect(file.type).toBe('image/png');
    expect([...new Uint8Array(await file.arrayBuffer())]).toEqual(png);
    expect(fetchMock).toHaveBeenCalledWith(url, { signal: expect.any(AbortSignal) });
    expect(openaiCalls()).toHaveLength(1);
    expect(openaiCalls()[0]![0]).toBe('https://api.openai.com/v1/responses');
    expect(JSON.parse(openaiCalls()[0]![1]!.body as string)).toEqual({
      model: 'gpt-6.1-terra',
      instructions: expect.any(String),
      input: [
        {
          role: 'user',
          content: [
            { type: 'input_image', image_url: 'data:image/png;base64,iVBORw==', detail: 'auto' },
          ],
        },
      ],
      reasoning: { effort: 'low' },
      max_output_tokens: 1_000,
    });
  });

  it('keeps a long caption within Discord’s message limit', async () => {
    openai = () => textResponse('a'.repeat(2_500));

    const response = await run();

    expect(response.data.content).toHaveLength(2_000);
  });

  it.each([
    { name: 'no options', options: null, attachments: null },
    { name: 'no resolved attachments', options: [imageOption], attachments: null },
    { name: 'an unknown attachment', options: [imageOption], attachments: { '701': attachment } },
    {
      name: 'a non-attachment option',
      options: [{ type: ApplicationCommandOptionType.String, name: 'image', value: '700' }],
      attachments: { '700': attachment },
    },
    {
      name: 'a non-image file',
      options: [imageOption],
      attachments: { '700': { ...attachment, content_type: 'application/pdf' } },
    },
    {
      name: 'an unsupported image type',
      options: [imageOption],
      attachments: { '700': { ...attachment, content_type: 'image/svg+xml' } },
    },
    {
      name: 'a missing content type',
      options: [imageOption],
      attachments: { '700': { ...attachment, content_type: undefined } },
    },
  ] as {
    name: string;
    options: APIApplicationCommandInteractionDataOption[] | null;
    attachments: Record<string, Partial<APIAttachment>> | null;
  }[])('rejects $name without downloading it', async ({ options, attachments }) => {
    expect(await run(options, attachments)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: {
        content: 'Upload a PNG, JPEG, WebP, or GIF image to caption.',
        flags: MessageFlags.Ephemeral,
      },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rejects an image too big to re-post without downloading it', async () => {
    const big = { ...attachment, size: 10 * 1024 * 1024 + 1 };

    expect(await run(undefined, { '700': big })).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: {
        content: 'That image is too big. Upload one under 10 MB.',
        flags: MessageFlags.Ephemeral,
      },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([{}, { OPENAI_API_KEY: '' }])('handles a missing API key: %j', async (env) => {
    expect(await run(undefined, undefined, env as Env)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Captions are not configured yet.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('handles a failed image download without calling OpenAI', async () => {
    download = () => new Response('Not found', { status: 404 });

    expect(await run()).toEqual(errorResponse);
    expect(openaiCalls()).toHaveLength(0);
  });

  it('handles an OpenAI error', async () => {
    openai = () => new Response('Unauthorized', { status: 401 });

    expect(await run()).toEqual(errorResponse);
  });

  it('handles an empty caption', async () => {
    openai = () => textResponse('   ');

    expect(await run()).toEqual(errorResponse);
  });

  it('gives up when the caller deadline passes', async () => {
    const deadline = new AbortController();
    openai = (init) =>
      new Promise((_resolve, reject) => {
        const signal = init.signal!;
        signal.addEventListener('abort', () => reject(signal.reason), { once: true });
      });

    const reply = run(undefined, undefined, undefined, deadline.signal);
    await vi.waitFor(() => expect(openaiCalls()).toHaveLength(1));
    deadline.abort();

    expect(await reply).toEqual(errorResponse);
    expect(openaiCalls()).toHaveLength(1);
  });
});
