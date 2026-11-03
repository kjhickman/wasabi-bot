import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  type APIApplicationCommandInteractionDataOption,
  type APIAttachment,
  type APIChatInputApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { CommandError } from '../src/commands/command.ts';
import { edit, imagine } from '../src/commands/image.ts';
import type { Env } from '../src/env.ts';

const fetchMock = vi.fn<typeof fetch>();
const url = 'https://cdn.discordapp.com/ephemeral-attachments/1/700/cat.png';
const png = [137, 80, 78, 71];
const env = { OPENAI_API_KEY: 'test-key' } as Env;
const attachment = { id: '700', filename: 'cat.png', content_type: 'image/png', size: 4, url };
const imageOption: APIApplicationCommandInteractionDataOption = {
  type: ApplicationCommandOptionType.Attachment,
  name: 'image',
  value: '700',
};
const failure = new CommandError("I couldn't make that image. Try again.");

let download: () => Response;
let openai: () => Response;

function prompt(value: string): APIApplicationCommandInteractionDataOption {
  return { type: ApplicationCommandOptionType.String, name: 'prompt', value };
}

function data(
  name: string,
  options: APIApplicationCommandInteractionDataOption[],
  attachments?: Record<string, Partial<APIAttachment>>,
) {
  return {
    id: '1',
    type: ApplicationCommandType.ChatInput,
    name,
    options,
    ...(attachments && { resolved: { attachments } }),
  } as APIChatInputApplicationCommandInteractionData;
}

function runImagine(options = [prompt('a frog in a hat')], commandEnv = env) {
  return imagine.execute(data('imagine', options), commandEnv, new AbortController().signal);
}

function runEdit(
  options = [imageOption, prompt('give it a party hat')],
  attachments: Record<string, Partial<APIAttachment>> = { '700': attachment },
  commandEnv = env,
) {
  return edit.execute(data('edit', options, attachments), commandEnv, new AbortController().signal);
}

function openaiCalls() {
  return fetchMock.mock.calls.filter(([input]) => String(input).startsWith('https://api.openai'));
}

beforeEach(() => {
  download = () => new Response(new Uint8Array(png), { headers: { 'Content-Type': 'image/png' } });
  openai = () => Response.json({ created: 0, data: [{ b64_json: btoa('new image') }] });
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (input) => (String(input) === url ? download() : openai()));
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

async function expectImage(response: Awaited<ReturnType<typeof runImagine>>, quote: string) {
  expect(response).toEqual({
    type: InteractionResponseType.ChannelMessageWithSource,
    data: { content: quote, allowed_mentions: { parse: [] } },
    files: [expect.any(File)],
  });
  const file = response.files![0]!;
  expect(file.name).toBe('image.png');
  expect(file.type).toBe('image/png');
  expect(await file.text()).toBe('new image');
}

describe('/imagine', () => {
  it('generates an image and posts it under the prompt without pinging anyone', async () => {
    const response = await runImagine([prompt('  a frog\n in a hat  ')]);

    await expectImage(response, '> a frog in a hat');
    expect(openaiCalls()).toHaveLength(1);
    expect(openaiCalls()[0]![0]).toBe('https://api.openai.com/v1/images/generations');
    expect(JSON.parse(openaiCalls()[0]![1]!.body as string)).toEqual({
      model: 'gpt-image-2.5-flare',
      size: 'auto',
      quality: 'medium',
      output_format: 'png',
      prompt: 'a frog\n in a hat',
    });
  });

  it.each([
    ['no prompt', []],
    ['a blank prompt', [prompt('   ')]],
  ])('asks for a description when given %s', async (_, options) => {
    await expect(runImagine(options)).rejects.toEqual(
      new CommandError('Describe the image you want.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('handles a missing API key', async () => {
    await expect(runImagine(undefined, {} as Env)).rejects.toEqual(
      new CommandError('Images are not configured yet.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('explains when the safety filter blocks the prompt', async () => {
    openai = () =>
      Response.json(
        {
          error: { message: 'Blocked', type: 'invalid_request_error', code: 'moderation_blocked' },
        },
        { status: 400 },
      );

    await expect(runImagine()).rejects.toEqual(
      new CommandError("OpenAI's safety filter blocked that. Try a different description."),
    );
  });

  it.each([
    ['OpenAI rejects the request', () => new Response('Unauthorized', { status: 401 })],
    ['OpenAI returns no image', () => Response.json({ created: 0, data: [] })],
  ])('apologizes when %s', async (_, response) => {
    openai = response;

    await expect(runImagine()).rejects.toEqual(failure);
  });
});

describe('/edit', () => {
  it('sends the downloaded image and prompt to OpenAI and posts the result', async () => {
    const response = await runEdit();

    await expectImage(response, '> give it a party hat');
    expect(fetchMock).toHaveBeenCalledWith(url, { signal: expect.any(AbortSignal) });
    expect(openaiCalls()).toHaveLength(1);
    expect(openaiCalls()[0]![0]).toBe('https://api.openai.com/v1/images/edits');
    const [input, init] = openaiCalls()[0]!;
    const form = await new Request(input, init).formData();
    expect(form.get('model')).toBe('gpt-image-2.5-flare');
    expect(form.get('prompt')).toBe('give it a party hat');
    expect(form.get('size')).toBe('auto');
    expect(form.get('quality')).toBe('medium');
    const image = form.get('image') as File;
    expect(image.name).toBe('cat.png');
    expect([...new Uint8Array(await image.arrayBuffer())]).toEqual(png);
  });

  it.each([
    ['no image', [prompt('add a hat')], {}],
    [
      'a GIF',
      [imageOption, prompt('add a hat')],
      { '700': { ...attachment, content_type: 'image/gif' } },
    ],
    [
      'a non-image file',
      [imageOption, prompt('add a hat')],
      { '700': { ...attachment, content_type: 'application/pdf' } },
    ],
  ])('rejects %s without downloading anything', async (_, options, attachments) => {
    await expect(runEdit(options, attachments)).rejects.toEqual(
      new CommandError('Upload a PNG, JPEG, or WebP image to edit.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rejects an image over 10 MB without downloading it', async () => {
    const big = { ...attachment, size: 10 * 1024 * 1024 + 1 };

    await expect(runEdit(undefined, { '700': big })).rejects.toEqual(
      new CommandError('That image is too big. Upload one under 10 MB.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('asks how to change the image when there is no prompt', async () => {
    await expect(runEdit([imageOption])).rejects.toEqual(
      new CommandError('Describe how to change the image.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('handles a missing API key', async () => {
    await expect(runEdit(undefined, undefined, {} as Env)).rejects.toEqual(
      new CommandError('Images are not configured yet.'),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('apologizes without calling OpenAI when the download fails', async () => {
    download = () => new Response('Not found', { status: 404 });

    await expect(runEdit()).rejects.toEqual(failure);
    expect(openaiCalls()).toHaveLength(0);
  });
});
