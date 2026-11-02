import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIApplicationCommandInteractionDataOption,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { conch } from '../src/commands/conch.ts';
import type { Env } from '../src/env.ts';

const fetchMock = vi.fn<typeof fetch>();
const question = 'Is the sky blue?';
const questionOption = (value: string): APIApplicationCommandInteractionDataOption => ({
  type: ApplicationCommandOptionType.String,
  name: 'question',
  value,
});
const options = [questionOption(question)];
const errorResponse = {
  type: InteractionResponseType.ChannelMessageWithSource,
  data: {
    content: "The conch couldn't answer. Try again.",
    flags: MessageFlags.Ephemeral,
  },
};

function ask(
  questionOptions: APIApplicationCommandInteractionDataOption[] | undefined,
  env = { OPENAI_API_KEY: 'test-key' } as Env,
  signal = new AbortController().signal,
) {
  const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'conch' } as const;
  return conch.execute(questionOptions ? { ...data, options: questionOptions } : data, env, signal);
}

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
  it('calls the Decisions API with yes, no, and unsure choices', async () => {
    fetchMock.mockResolvedValue(Response.json({ answers: [{ type: 'choice', choice: 'yes' }] }));

    await ask(options);

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

    expect(await ask(options)).toEqual({
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

    expect(await ask(options)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content },
    });
  });

  it.each<{ options: APIApplicationCommandInteractionDataOption[] | undefined }>([
    { options: undefined },
    { options: [] },
    { options: [questionOption('')] },
    { options: [questionOption('   ')] },
    { options: [{ type: ApplicationCommandOptionType.Integer, name: 'question', value: 42 }] },
  ])('rejects a missing or invalid question: %j', async ({ options: invalidOptions }) => {
    expect(await ask(invalidOptions)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Ask the conch a yes/no question.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([{}, { OPENAI_API_KEY: '' }])('handles a missing API key: %j', async (env) => {
    expect(await ask(options, env as Env)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'The conch is not configured yet.', flags: MessageFlags.Ephemeral },
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([401, 429, 500])('handles HTTP %s without retrying', async (status) => {
    fetchMock.mockResolvedValue(new Response('Upstream error', { status }));

    expect(await ask(options)).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('handles a network failure', async () => {
    fetchMock.mockRejectedValue(new TypeError('Network error'));

    expect(await ask(options)).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('handles invalid JSON', async () => {
    fetchMock.mockResolvedValue(new Response('not JSON'));

    expect(await ask(options)).toEqual(errorResponse);
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

    expect(await ask(options)).toEqual(errorResponse);
  });

  it('gives up when the caller deadline passes', async () => {
    const deadline = new AbortController();
    fetchMock.mockImplementation(
      (_input, init) =>
        new Promise((_resolve, reject) => {
          const signal = init!.signal!;
          signal.addEventListener('abort', () => reject(signal.reason), { once: true });
        }),
    );

    const answer = ask(options, undefined, deadline.signal);
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledOnce());
    deadline.abort();

    expect(await answer).toEqual(errorResponse);
    expect(fetchMock.mock.calls[0]![1]!.signal!.aborted).toBe(true);
  });

  it.each([200, 500])('gives up on a stalled HTTP %s response body', async (status) => {
    const deadline = new AbortController();
    fetchMock.mockImplementation(async (_input, init) => {
      const signal = init!.signal!;
      const body = new ReadableStream({
        start(controller) {
          signal.addEventListener('abort', () => controller.error(signal.reason), { once: true });
        },
      });
      setTimeout(() => deadline.abort(), 0);
      return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
    });

    expect(await ask(options, undefined, deadline.signal)).toEqual(errorResponse);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]![1]!.signal!.aborted).toBe(true);
  });
});
