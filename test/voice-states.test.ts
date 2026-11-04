import type { APIUser, GatewayDispatchPayload } from 'discord-api-types/v10';
import { beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import type { Env } from '../src/env.ts';
import { handleEvent } from '../src/gateway/events.ts';
import { countListeners } from '../src/gateway/voice-states.ts';
import { sqlStorage } from './fixtures/sql.ts';

const BOT = { id: '100', username: 'wasabibot' } as APIUser;
const voiceEvent = vi.fn();
const getByName = vi.fn(() => ({ voiceEvent }));
const env = { RADIO_PLAYER: { getByName } } as unknown as Env;
let sql: SqlStorage;

function dispatch(t: string, d: object) {
  const packet = { op: 0, s: 1, t, d } as GatewayDispatchPayload;
  return handleEvent(packet, { bot: BOT, token: 'bot-token', env, sql });
}

function listeners(channelId = '400') {
  return countListeners(sql, '300', channelId, BOT.id);
}

function guild(...states: [userId: string, channelId: string][]) {
  return dispatch('GUILD_CREATE', {
    id: '300',
    voice_states: states.map(([user_id, channel_id]) => ({ user_id, channel_id })),
  });
}

beforeEach(() => {
  sql = sqlStorage();
  voiceEvent.mockReset();
  getByName.mockClear();
});

function voice(userId: string, channelId: string | null) {
  return dispatch('VOICE_STATE_UPDATE', {
    guild_id: '300',
    user_id: userId,
    channel_id: channelId,
  });
}

describe('voice states', () => {
  it('counts the people in each channel from GUILD_CREATE, leaving out the bot', async () => {
    await guild(['100', '400'], ['201', '400'], ['202', '400'], ['203', '401']);

    expect(listeners()).toBe(2);
    expect(listeners('401')).toBe(1);
  });

  it('follows people as they join, move, and leave', async () => {
    await guild(['201', '400']);

    await dispatch('VOICE_STATE_UPDATE', { guild_id: '300', user_id: '202', channel_id: '400' });
    expect(listeners()).toBe(2);

    await dispatch('VOICE_STATE_UPDATE', { guild_id: '300', user_id: '201', channel_id: '401' });
    await dispatch('VOICE_STATE_UPDATE', { guild_id: '300', user_id: '202', channel_id: null });
    expect(listeners()).toBe(0);
    expect(listeners('401')).toBe(1);
  });

  it.each([
    ['a new session starts', () => dispatch('READY', {})],
    ['the bot leaves the guild', () => dispatch('GUILD_DELETE', { id: '300' })],
    [
      'the guild becomes unavailable',
      () => dispatch('GUILD_CREATE', { id: '300', unavailable: true }),
    ],
  ])('forgets what it knew when %s', async (_, event) => {
    await guild(['201', '400']);

    await event();

    expect(listeners()).toBe(0);
  });
});

describe('radio voice events', () => {
  it("forwards the bot's voice state and voice server to the guild's radio", async () => {
    await dispatch('VOICE_STATE_UPDATE', {
      guild_id: '300',
      user_id: '100',
      channel_id: '400',
      session_id: 'voice-session',
    });
    await dispatch('VOICE_SERVER_UPDATE', {
      guild_id: '300',
      token: 'voice-token',
      endpoint: 'voice.discord.media',
    });

    expect(getByName.mock.calls).toEqual([['300'], ['300']]);
    expect(voiceEvent.mock.calls.map(([packet]) => packet.t)).toEqual([
      'VOICE_STATE_UPDATE',
      'VOICE_SERVER_UPDATE',
    ]);
  });

  it("keeps other people's voice states to itself", async () => {
    await voice('201', '400');

    expect(voiceEvent).not.toHaveBeenCalled();
  });
});
