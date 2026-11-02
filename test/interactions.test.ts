import { InteractionResponseType, MessageFlags, type APIInteraction } from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import type { Env } from '../src/env.ts';
import { createInteractionsTable, handleInteraction } from '../src/gateway/interactions.ts';
import { takeUsage } from '../src/usage.ts';
import { sqlStorage } from './fixtures/sql.ts';

const env = { OPENAI_API_KEY: 'test-key' } as Env;
const fetchMock = vi.fn<typeof fetch>();
let sql: SqlStorage;
let nextId = 0;

function conch(user: object) {
  const id = ((BigInt(Date.now()) - 1_420_070_400_000n) << 22n) + BigInt(nextId++);
  return {
    type: 2,
    id: id.toString(),
    application_id: '900',
    token: 'interaction-token',
    ...user,
    data: {
      type: 1,
      name: 'conch',
      options: [{ type: 3, name: 'question', value: 'Is the sky blue?' }],
    },
  } as unknown as APIInteraction;
}

function callbacks() {
  return fetchMock.mock.calls
    .filter(([url]) => String(url).endsWith('/callback'))
    .map(([, init]) => JSON.parse(init!.body as string));
}

function requests() {
  return sql.exec("SELECT user_id FROM usage WHERE kind = 'text'").toArray();
}

beforeEach(() => {
  sql = sqlStorage();
  createInteractionsTable(sql);
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url) =>
    String(url).endsWith('/v1/decisions')
      ? Response.json({ answers: [{ type: 'choice', choice: 'yes' }] })
      : new Response(null, { status: 204 }),
  );
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('daily limits for slash commands', () => {
  it.each([
    ['in a server', { member: { user: { id: '200' } } }],
    ['in a DM', { user: { id: '200' } }],
  ])('counts a command that calls OpenAI %s', async (_, user) => {
    await handleInteraction(conch(user), env, sql);

    expect(callbacks()).toEqual([
      { type: InteractionResponseType.ChannelMessageWithSource, data: { content: 'Yes' } },
    ]);
    expect(requests()).toEqual([{ user_id: '200' }]);
  });

  it('does not count a command that fails', async () => {
    await handleInteraction(conch({ user: { id: '200' } }), {} as Env, sql);

    expect(callbacks()[0].data.flags).toBe(MessageFlags.Ephemeral);
    expect(requests()).toEqual([]);
  });

  it('refuses privately over the limit without running the command', async () => {
    for (let i = 0; i < 100; i++) takeUsage(sql, 'text', '200');

    await handleInteraction(conch({ user: { id: '200' } }), env, sql);

    expect(callbacks()).toEqual([
      {
        type: InteractionResponseType.ChannelMessageWithSource,
        data: {
          content: expect.stringMatching(/^You've used all 100 of your requests for today\./),
          flags: MessageFlags.Ephemeral,
        },
      },
    ]);
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('openai'))).toBe(false);
  });
});
