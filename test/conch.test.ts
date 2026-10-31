import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { conch } from '../src/commands/conch.ts';

const fetchMock = vi.fn<typeof fetch>();
const question = 'Is the sky blue?';
const options = [
  { type: ApplicationCommandOptionType.String as const, name: 'question', value: question },
];
const errorResponse = {
  type: InteractionResponseType.ChannelMessageWithSource,
  data: {
    content: "The conch couldn't answer. Try again.",
    flags: MessageFlags.Ephemeral,
  },
};

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(Math, 'random').mockReturnValue(0.5);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('/conch', () => {
  it('defines a required question option', () => {
    expect(conch.definition).toEqual({
      name: 'conch',
      description: expect.any(String),
      type: ApplicationCommandType.ChatInput,
      options: [
        {
          type: ApplicationCommandOptionType.String,
          name: 'question',
          description: expect.any(String),
          required: true,
          min_length: 1,
        },
      ],
    });
  });

  it('calls the Decisions API with yes, no, and unsure choices', async () => {
    fetchMock.mockResolvedValue(Response.json({ answers: [{ type: 'choice', choice: 'yes' }] }));

    await conch.execute(options, 'test-key');

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledWith(
      'https://api.openai.com/v1/decisions',
      expect.objectContaining({
        method: 'POST',
        headers: expect.any(Headers),
        body: expect.any(String),
        signal: expect.any(AbortSignal),
      }),
    );
    const headers = new Headers(fetchMock.mock.calls[0]![1]!.headers);
    expect(headers.get('Authorization')).toBe('Bearer test-key');
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(JSON.parse(fetchMock.mock.calls[0]![1]!.body as string)).toEqual({
      model: 'gpt-6-luna',
      input: question,
      questions: [
        {
          type: 'choice',
          name: 'answer',
          instructions: expect.any(String),
          choices: [
            { value: 'yes', description: expect.any(String) },
            { value: 'no', description: expect.any(String) },
            { value: 'unsure', description: expect.any(String) },
          ],
        },
      ],
    });
  });

  it.each([
    ['yes', 'Yes'],
    ['no', 'No'],
  ])('replies directly with %s without randomizing', async (choice, content) => {
    fetchMock.mockResolvedValue(Response.json({ answers: [{ type: 'choice', choice }] }));

    expect(await conch.execute(options, 'test-key')).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content },
    });
  });

  it.each([
    [0, 'Yes'],
    [0.2, 'No'],
    [0.4, "I don't think so"],
    [0.6, 'Maybe'],
    [0.999999, 'Try asking again'],
  ] as const)('maps unsure to %s → %s', async (random, content) => {
    vi.mocked(Math.random).mockReturnValue(random);
    fetchMock.mockResolvedValue(Response.json({ answers: [{ type: 'choice', choice: 'unsure' }] }));

    expect(await conch.execute(options, 'test-key')).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content },
    });
  });

  it.each([
    { options: undefined },
    { options: [] },
    { options: [{ ...options[0]!, value: '' }] },
    { options: [{ ...options[0]!, value: '   ' }] },
  ])('rejects a missing or blank question: %j', async ({ options: invalidOptions }) => {
    expect(await conch.execute(invalidOptions, 'test-key')).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Ask the conch a yes/no question.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rejects a non-string question', async () => {
    expect(
      await conch.execute(
        [{ type: ApplicationCommandOptionType.Integer, name: 'question', value: 42 }],
        'test-key',
      ),
    ).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Ask the conch a yes/no question.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([undefined, ''])('handles a missing API key: %j', async (apiKey) => {
    expect(await conch.execute(options, apiKey)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'The conch is not configured yet.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([401, 429, 500])('handles HTTP %s without randomizing', async (status) => {
    fetchMock.mockResolvedValue(new Response('Upstream error', { status }));

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('handles a network failure', async () => {
    fetchMock.mockRejectedValue(new TypeError('Network error'));

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('handles invalid JSON', async () => {
    fetchMock.mockResolvedValue(new Response('not JSON'));

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
  });

  it.each([
    null,
    {},
    { answers: [] },
    { answers: [null] },
    { answers: [{ type: 'refusal' }] },
    { answers: [{ type: 'predicate', probability: 1 }] },
    { answers: [{ type: 'choice', choice: 'maybe' }] },
    { answers: [{ type: 'choice', choice: true }] },
  ])('handles an invalid or refused decision without randomizing: %j', async (body) => {
    fetchMock.mockResolvedValue(Response.json(body));

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
  });

  it('aborts a slow request after two seconds instead of deferring', async () => {
    const timeoutSpy = vi.spyOn(AbortSignal, 'timeout');
    fetchMock.mockImplementation(
      (_input, init) =>
        new Promise((_resolve, reject) => {
          const signal = init!.signal!;
          signal.addEventListener('abort', () => reject(signal.reason), { once: true });
        }),
    );

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
    expect(timeoutSpy).toHaveBeenCalledWith(2_000);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]![1]!.signal!.aborted).toBe(true);
  });

  it.each([200, 500])('aborts a stalled HTTP %s response body without retrying', async (status) => {
    fetchMock.mockImplementation(async (_input, init) => {
      const signal = init!.signal!;
      const body = new ReadableStream({
        start(controller) {
          signal.addEventListener('abort', () => controller.error(signal.reason), { once: true });
        },
      });
      return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
    });

    expect(await conch.execute(options, 'test-key')).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]![1]!.signal!.aborted).toBe(true);
  });
});
