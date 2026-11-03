import { InteractionResponseType, MessageFlags, type APIInteraction } from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { conch as conchCommand } from '../src/commands/conch.ts';
import type { Env } from '../src/env.ts';
import { handleInteraction } from '../src/gateway/interactions.ts';
import { takeUsage } from '../src/usage.ts';
import { sqlStorage } from './fixtures/sql.ts';

const env = { OPENAI_API_KEY: 'test-key' } as Env;
const fetchMock = vi.fn<typeof fetch>();
let sql: SqlStorage;
let nextId = 0;

function interaction(user: object, data: object) {
  const id = ((BigInt(Date.now()) - 1_420_070_400_000n) << 22n) + BigInt(nextId++);
  return {
    type: 2,
    id: id.toString(),
    application_id: '900',
    token: 'interaction-token',
    ...user,
    data: { type: 1, ...data },
  } as unknown as APIInteraction;
}

function conch(user: object) {
  return interaction(user, {
    name: 'conch',
    options: [{ type: 3, name: 'question', value: 'Is the sky blue?' }],
  });
}

function callbacks() {
  return fetchMock.mock.calls
    .filter(([url]) => String(url).endsWith('/callback'))
    .map(([, init]) => JSON.parse(init!.body as string));
}

function edits() {
  return fetchMock.mock.calls
    .filter(([url]) => String(url).endsWith('/messages/%40original'))
    .map(([, init]) => JSON.parse((init!.body as FormData).get('payload_json') as string));
}

function requests() {
  return sql.exec("SELECT user_id FROM usage WHERE kind = 'text'").toArray();
}

beforeEach(() => {
  sql = sqlStorage();
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

  it('logs and does not count a command that crashes', async () => {
    vi.spyOn(conchCommand, 'execute').mockRejectedValue(new Error('boom'));

    await handleInteraction(conch({ user: { id: '200' } }), env, sql);

    expect(console.error).toHaveBeenCalledWith('Gateway command failed', 'conch', 'boom');
    expect(requests()).toEqual([]);
  });

  it('edits a deferred command’s error into its reply without counting it', async () => {
    await handleInteraction(interaction({ user: { id: '200' } }, { name: 'caption' }), env, sql);

    expect(callbacks()).toEqual([
      { type: InteractionResponseType.DeferredChannelMessageWithSource },
    ]);
    expect(edits()).toEqual([
      { content: 'Upload a PNG, JPEG, WebP, or GIF image to caption.', attachments: [] },
    ]);
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
