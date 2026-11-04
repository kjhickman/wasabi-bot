import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import type { Env } from '../src/env.ts';
import { RadioCallback, RadioPlayer } from '../src/radio/player.ts';

vi.mock('cloudflare:workers', async (importOriginal) => {
  class Base {
    ctx: unknown;
    env: unknown;
    constructor(ctx: unknown, env: unknown) {
      this.ctx = ctx;
      this.env = env;
    }
  }
  return { ...(await importOriginal<object>()), DurableObject: Base, WorkerEntrypoint: Base };
});

const STATION = { name: '101 SMOOTH JAZZ', url: 'http://jking.cdnstream1.com/b22139_128mp3' };
const LEAVE = { guild_id: '300', channel_id: null, self_mute: false, self_deaf: false };
const KILLED = new Error('Container exited with unexpected exit code: 137');

class FakeContainer {
  running = false;
  requests: { path: string; body: unknown }[] = [];
  replies: Record<string, () => Response> = {
    '/status': () => Response.json({ active: true }),
  };
  private exit = Promise.withResolvers<void>();
  start = vi.fn(() => {
    if (this.running) throw new Error('The container is already running.');
    this.running = true;
    this.exit = Promise.withResolvers<void>();
  });
  // Locally, destroying a container reports it as killed.
  destroy = vi.fn(async () => this.crash(KILLED));
  monitor = vi.fn(() => this.exit.promise);
  setInactivityTimeout = vi.fn(async () => {});
  interceptOutboundHttp = vi.fn(async () => {});

  getTcpPort() {
    return {
      fetch: async (url: string, init: RequestInit) => {
        const { pathname } = new URL(url);
        this.requests.push({ path: pathname, body: init.body && JSON.parse(init.body as string) });
        return this.replies[pathname]?.() ?? Response.json({ ok: true });
      },
    };
  }

  crash(error?: Error) {
    this.running = false;
    if (error) this.exit.reject(error);
    else this.exit.resolve();
  }

  paths() {
    return this.requests.map(({ path }) => path);
  }
}

const gatewayStub = { updateVoiceState: vi.fn(), listeners: vi.fn() };
const env = { DISCORD_GATEWAY: { getByName: () => gatewayStub } } as unknown as Env;
let container: FakeContainer;
let kv: Map<string, unknown>;
let alarm: number | null;
let pending: Promise<unknown>[];
let player: RadioPlayer;

function createPlayer() {
  const state = {
    container,
    exports: { RadioCallback: (options: object) => ({ callback: options }) },
    storage: {
      kv: {
        get: (key: string) => kv.get(key),
        put: (key: string, value: unknown) => kv.set(key, structuredClone(value)),
        delete: (key: string) => kv.delete(key),
      },
      setAlarm: async (time: number) => {
        alarm = time;
      },
      deleteAlarm: async () => {
        alarm = null;
      },
    },
    waitUntil: (promise: Promise<unknown>) => pending.push(promise),
    blockConcurrencyWhile: (callback: () => Promise<unknown>) => callback(),
  };
  return new RadioPlayer(state as unknown as DurableObjectState, env);
}

async function settle() {
  await Promise.all(pending);
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  container = new FakeContainer();
  kv = new Map();
  alarm = null;
  pending = [];
  gatewayStub.updateVoiceState.mockReset().mockResolvedValue(true);
  gatewayStub.listeners.mockReset().mockResolvedValue(1);
  player = createPlayer();
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('RadioPlayer', () => {
  it('starts a container with a callback for its guild, then plays the station', async () => {
    expect(await player.play('300', '400', STATION)).toBeUndefined();

    expect(container.interceptOutboundHttp).toHaveBeenCalledExactlyOnceWith('player.internal', {
      callback: { props: { guildId: '300' } },
    });
    expect(container.start).toHaveBeenCalledExactlyOnceWith({ enableInternet: true });
    expect(container.requests).toEqual([
      { path: '/status', body: undefined },
      { path: '/play', body: { guildId: '300', channelId: '400', url: STATION.url } },
    ]);
    expect(alarm).toBe(Date.now() + 30_000);
  });

  it('starts one container for plays that arrive together', async () => {
    const results = await Promise.all([
      player.play('300', '400', STATION),
      player.play('300', '400', STATION),
    ]);

    expect(results).toEqual([undefined, undefined]);
    expect(container.start).toHaveBeenCalledOnce();
    expect(container.running).toBe(true);
  });

  it('switches stations in the same container', async () => {
    await player.play('300', '400', STATION);

    await player.play('300', '400', { name: 'Jazz FM', url: 'https://jazz.example/stream' });

    expect(container.start).toHaveBeenCalledOnce();
    expect(container.paths()).toEqual(['/status', '/play', '/play']);
  });

  it("passes on the container's reason and leaves when a station won't play", async () => {
    container.replies['/play'] = () =>
      Response.json({ error: "That station isn't streaming right now." });

    expect(await player.play('300', '400', STATION)).toBe(
      "That station isn't streaming right now.",
    );
    expect(container.destroy).toHaveBeenCalledOnce();
    expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(LEAVE);
    expect(alarm).toBeNull();
  });

  it('gives a general reason when the container crashes mid-request', async () => {
    container.replies['/play'] = () => new Response(null, { status: 500 });

    expect(await player.play('300', '400', STATION)).toBe("The radio couldn't play that station.");
  });

  it('stops: destroys the container and leaves the channel once', async () => {
    await player.play('300', '400', STATION);

    expect(await player.stop()).toBe(true);
    await settle();

    expect(container.destroy).toHaveBeenCalledOnce();
    expect(container.running).toBe(false);
    expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(LEAVE);
    expect(alarm).toBeNull();
    expect(console.error).not.toHaveBeenCalled();
    expect(await player.stop()).toBe(false);
  });

  it.each([
    ['exits', undefined, 'exited'],
    ['is killed', KILLED, expect.objectContaining({ message: KILLED.message })],
  ])(
    'leaves the channel and logs it when its container %s on its own',
    async (_, error, logged) => {
      await player.play('300', '400', STATION);

      container.crash(error);
      await settle();

      expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(LEAVE);
      expect(alarm).toBeNull();
      expect(console.error).toHaveBeenCalledExactlyOnceWith(
        expect.objectContaining({
          message: 'Radio container stopped unexpectedly',
          guildId: '300',
          error: logged,
        }),
      );
    },
  );

  it('watches a container that kept running through a restart', async () => {
    container.start();
    kv.set('session', { guildId: '300', channelId: '400', startedAt: Date.now() });

    createPlayer();
    container.crash();

    await vi.waitFor(() =>
      expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(LEAVE),
    );
    expect(container.setInactivityTimeout).toHaveBeenCalledOnce();
  });

  describe('voice events', () => {
    const state = {
      t: 'VOICE_STATE_UPDATE',
      d: { guild_id: '300', user_id: '100', channel_id: '401' },
    } as never;

    it('passes them to a running container, and follows the bot when it is moved', async () => {
      await player.play('300', '400', STATION);

      await player.voiceEvent(state);
      await player.alarm();

      expect(container.requests.at(-2)).toEqual({ path: '/voice', body: state });
      expect(gatewayStub.listeners).toHaveBeenCalledExactlyOnceWith('300', '401');
    });

    it('drops them without a container', async () => {
      await player.voiceEvent(state);

      expect(container.requests).toEqual([]);
    });
  });

  describe('watchdog', () => {
    beforeEach(async () => {
      await player.play('300', '400', STATION);
    });

    it('keeps playing while someone listens', async () => {
      vi.advanceTimersByTime(30_000);

      await player.alarm();

      expect(container.running).toBe(true);
      expect(alarm).toBe(Date.now() + 30_000);
    });

    it('leaves as soon as it finds nobody listening', async () => {
      gatewayStub.listeners.mockResolvedValue(0);

      await player.alarm();

      expect(container.running).toBe(false);
      expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(LEAVE);
    });

    it('leaves when the container has given up on the voice connection or stream', async () => {
      container.replies['/status'] = () => Response.json({ active: false });

      await player.alarm();

      expect(container.running).toBe(false);
    });

    it('leaves after 12 hours no matter what', async () => {
      vi.advanceTimersByTime(12 * 60 * 60 * 1_000);

      await player.alarm();

      expect(container.running).toBe(false);
    });

    it('keeps watching when a check fails', async () => {
      gatewayStub.listeners.mockRejectedValue(new Error('Gateway unavailable'));

      await player.alarm();

      expect(container.running).toBe(true);
      expect(alarm).toBe(Date.now() + 30_000);
    });
  });
});

describe('RadioCallback', () => {
  function callback() {
    const ctx = { props: { guildId: '300' } };
    return new RadioCallback(ctx as unknown as ExecutionContext<{ guildId: string }>, env);
  }

  function post(payload: object) {
    return callback().fetch(
      new Request('http://player.internal/gateway', {
        method: 'POST',
        body: JSON.stringify(payload),
      }),
    );
  }

  const join = { guild_id: '300', channel_id: '400', self_mute: false, self_deaf: true };

  it("sends the container's voice state update through the Gateway", async () => {
    const response = await post({ op: 4, d: join });

    expect(response.status).toBe(204);
    expect(gatewayStub.updateVoiceState).toHaveBeenCalledExactlyOnceWith(join);
  });

  it('reports when the Gateway is not connected', async () => {
    gatewayStub.updateVoiceState.mockResolvedValue(false);

    expect((await post({ op: 4, d: join })).status).toBe(503);
  });

  it.each([
    ['another guild', { op: 4, d: { ...join, guild_id: '301' } }],
    ['another opcode', { op: 2, d: { token: 'stolen' } }],
  ])('refuses a payload for %s', async (_, payload) => {
    expect((await post(payload)).status).toBe(403);
    expect(gatewayStub.updateVoiceState).not.toHaveBeenCalled();
  });

  it('ignores anything but a POST to /gateway', async () => {
    const response = await callback().fetch(new Request('http://player.internal/'));

    expect(response.status).toBe(404);
  });
});
