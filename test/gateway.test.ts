import { Log, LogLevel, Miniflare } from 'miniflare';
import { build } from 'vite-plus';
import { afterEach, assert, beforeAll, describe, expect, it, vi } from 'vite-plus/test';

let gatewayCode: string;
let discordCode: string;
let runtime: Miniflare | undefined;

interface DiscordState {
  packets: {
    op: number;
    receivedAt: number;
    d: { presence?: { status: string }; session_id?: string; seq?: number };
  }[];
  urls: string[];
  callbacks: { type: number; data: { content: string } }[];
  replies: { content: string; message_reference: { message_id: string } }[];
  edits: { method: string; path: string; body: unknown; files: unknown[]; at: number }[];
  decisions: number;
  discoveries: number;
  connections: number;
  callbackTimes: number[];
  closeCodes: number[];
}

interface Status {
  running: boolean;
  connected: boolean;
  leaseExpiresAt: string | null;
  retryAt: string | null;
  error: string | null;
  lastEvent: string | null;
}

beforeAll(async () => {
  [gatewayCode, discordCode] = (await Promise.all(
    ['test/fixtures/gateway-worker.ts', 'test/fixtures/discord.ts'].map(async (file) => {
      const result = await build({
        configFile: false,
        envFile: false,
        logLevel: 'silent',
        ssr: { noExternal: true },
        build: {
          ssr: file,
          write: false,
          target: 'esnext',
          rolldownOptions: {
            external: ['cloudflare:workers', /^node:/],
            output: { codeSplitting: false },
          },
        },
      });
      assert('output' in result);
      const [chunk] = result.output;
      assert(chunk?.type === 'chunk');
      return chunk.code;
    }),
  )) as [string, string];
});

afterEach(async () => {
  await runtime?.dispose();
  runtime = undefined;
});

function createRuntime(mode: 'dev' | 'prod' = 'dev') {
  runtime = new Miniflare({
    log: new Log(LogLevel.NONE),
    telemetry: { enabled: false },
    workers: [
      {
        config: {
          name: 'bot',
          compatibilityDate: '2026-10-19',
          compatibilityFlags: ['nodejs_compat'],
          manifest: {
            mainModule: 'index.js',
            modules: { 'index.js': { type: 'esm', contents: gatewayCode } },
          },
          env: {
            DISCORD_GATEWAY: { type: 'durable-object', worker: 'bot', exportName: 'TestGateway' },
            GATEWAY_MODE: { type: 'json', value: mode },
            GATEWAY_CONTROL_TOKEN: { type: 'json', value: 'control-token' },
            DISCORD_BOT_TOKEN: { type: 'json', value: 'MTAw.bot-token' },
            OPENAI_API_KEY: { type: 'json', value: 'openai-token' },
          },
          exports: { TestGateway: { type: 'durable-object', storage: 'sqlite' } },
        },
        dev: { outboundService: { type: 'worker', worker: 'discord' } },
      },
      {
        config: {
          name: 'discord',
          compatibilityDate: '2026-10-19',
          manifest: {
            mainModule: 'index.js',
            modules: { 'index.js': { type: 'esm', contents: discordCode } },
          },
          env: {
            DISCORD: { type: 'durable-object', worker: 'discord', exportName: 'FakeDiscord' },
          },
          exports: { FakeDiscord: { type: 'durable-object', storage: 'sqlite' } },
        },
      },
    ],
  });
}

async function control(action: string, token = 'control-token') {
  return runtime!.dispatchFetch(`https://bot.test/gateway/${action}`, {
    method: action === 'status' ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${token}` },
  });
}

async function status(): Promise<Status> {
  return (await control('status')).json() as Promise<Status>;
}

async function fake(path: string, body?: unknown) {
  const discord = (await runtime!.getWorker('discord')) as unknown as { fetch: typeof fetch };
  return discord.fetch(
    `https://fake.test/${path}`,
    body === undefined
      ? undefined
      : {
          method: 'POST',
          body: JSON.stringify(body),
        },
  );
}

async function discordState(): Promise<DiscordState> {
  return (await fake('state')).json() as Promise<DiscordState>;
}

async function reconcile() {
  await (await runtime!.dispatchFetch('https://bot.test/_test/reconcile')).text();
}

async function connected() {
  try {
    await vi.waitFor(async () => expect((await status()).connected).toBe(true), { timeout: 8_000 });
  } catch (cause) {
    throw new Error(JSON.stringify({ status: await status(), discord: await discordState() }), {
      cause,
    });
  }
}

function conchInteraction(age = 0) {
  const createdAt = Date.now() - age;
  const id = ((BigInt(createdAt) - 1_420_070_400_000n) << 22n).toString();
  return {
    op: 0,
    t: 'INTERACTION_CREATE',
    s: 2,
    d: {
      type: 2,
      id,
      token: 'interaction-token',
      data: {
        type: 1,
        name: 'conch',
        options: [{ type: 3, name: 'question', value: 'Is the sky blue?' }],
      },
    },
  };
}

function captionInteraction() {
  const id = ((BigInt(Date.now()) - 1_420_070_400_000n) << 22n).toString();
  return {
    op: 0,
    t: 'INTERACTION_CREATE',
    s: 2,
    d: {
      type: 2,
      id,
      application_id: '900',
      token: 'interaction-token',
      data: {
        type: 1,
        name: 'caption',
        options: [{ type: 11, name: 'image', value: '700' }],
        resolved: {
          attachments: {
            '700': {
              id: '700',
              filename: 'cat.png',
              content_type: 'image/png',
              size: 4,
              url: 'https://cdn.discordapp.com/ephemeral-attachments/1/700/cat.png',
            },
          },
        },
      },
    },
  };
}

function message(id: string, s: number, mentions = [{ id: '100', username: 'devbot' }]) {
  return {
    op: 0,
    t: 'MESSAGE_CREATE',
    s,
    d: {
      id,
      channel_id: '300',
      content: '<@100> hi',
      author: { id: '400', username: 'kyle' },
      mentions,
    },
  };
}

describe('Gateway in workerd', () => {
  it('keeps dev stopped by default, including after scheduled checks', async () => {
    createRuntime();
    await reconcile();

    expect(await status()).toMatchObject({
      running: false,
      connected: false,
      leaseExpiresAt: null,
    });
    expect((await discordState()).discoveries).toBe(0);
    expect(await (await runtime!.dispatchFetch('https://bot.test/_test/alarm')).json()).toBeNull();
  });

  it('rejects control requests before reaching the Durable Object', async () => {
    createRuntime();
    const response = await control('start', 'wrong-token');

    expect(response.status).toBe(401);
    expect((await discordState()).discoveries).toBe(0);
  });

  it('starts and renews a one-hour dev session without a duplicate connection', async () => {
    createRuntime();
    const before = Date.now();
    expect((await control('start')).status).toBe(200);
    await connected();
    const first = await status();
    expect(Date.parse(first.leaseExpiresAt!)).toBeGreaterThanOrEqual(before + 3_600_000);
    expect(Date.parse(first.leaseExpiresAt!)).toBeLessThan(Date.now() + 3_601_000);
    await (await control('start')).text();
    await reconcile();

    const state = await discordState();
    expect(state.packets.filter((packet) => packet.op === 2)).toHaveLength(1);
    expect(state.packets.find((packet) => packet.op === 2)!.d.presence!.status).toBe('online');
    expect(state.connections).toBe(1);
    expect((await status()).running).toBe(true);
    expect(JSON.stringify(await status())).not.toMatch(
      /bot-token|control-token|fake-session|openai-token/,
    );
  }, 15_000);

  it('stops dev and does not let the scheduler or a restart re-enable it', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await control('stop')).text();
    await (await runtime!.dispatchFetch('https://bot.test/_test/crash')).text();
    await reconcile();

    expect(await status()).toMatchObject({ running: false, connected: false });
    expect(await (await runtime!.dispatchFetch('https://bot.test/_test/alarm')).json()).toBeNull();
    await vi.waitFor(async () => expect((await discordState()).connections).toBe(0));
    expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(1);
  }, 15_000);

  it('expires a dev lease durably without reconnecting after restart', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await runtime!.dispatchFetch('https://bot.test/_test/expire')).text();
    await reconcile();
    await reconcile();

    expect(await status()).toMatchObject({
      running: false,
      connected: false,
      leaseExpiresAt: null,
    });
    expect((await discordState()).discoveries).toBe(1);
    expect(await (await runtime!.dispatchFetch('https://bot.test/_test/alarm')).json()).toBeNull();
  }, 15_000);

  it('starts production automatically, but respects an explicit stop', async () => {
    createRuntime('prod');
    await reconcile();
    await connected();
    expect((await status()).leaseExpiresAt).toBeNull();
    await (await control('stop')).text();
    await reconcile();

    expect((await status()).running).toBe(false);
    expect((await discordState()).discoveries).toBe(1);
  }, 15_000);

  it('restores the resume URL, session and sequence after a DO reset', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (
      await fake('emit', {
        op: 0,
        t: 'MESSAGE_CREATE',
        s: 5,
        d: { author: { bot: false }, mentions: [] },
      })
    ).text();
    await vi.waitFor(async () => expect((await status()).lastEvent).toBe('MESSAGE_CREATE'));
    await (await runtime!.dispatchFetch('https://bot.test/_test/crash')).text();
    await reconcile();
    await connected();

    const state = await discordState();
    expect(state.packets.filter((packet) => packet.op === 2)).toHaveLength(1);
    expect(state.packets.find((packet) => packet.op === 6)!.d).toMatchObject({
      session_id: 'fake-session-1',
      seq: 5,
    });
    expect(state.urls[1]).toContain('resume.discord.gg');
  }, 15_000);

  it('handles /conch directly and suppresses duplicate interaction side effects', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    const interaction = conchInteraction();
    await (await fake('emit', interaction)).text();
    await vi.waitFor(async () => expect((await discordState()).callbacks).toHaveLength(1));
    await (await fake('emit', { ...interaction, s: 3 })).text();
    await vi.waitFor(async () => expect((await status()).lastEvent).toBe('INTERACTION_CREATE'));
    await (await runtime!.dispatchFetch('https://bot.test/_test/crash')).text();
    await reconcile();
    await connected();
    await (await fake('emit', { ...interaction, s: 4 })).text();
    await vi.waitFor(async () => expect((await status()).lastEvent).toBe('INTERACTION_CREATE'));

    const state = await discordState();
    expect(state.decisions).toBe(1);
    expect(state.callbacks).toEqual([{ type: 4, data: { content: 'Yes' } }]);
  }, 15_000);

  it('defers /caption within the response window and edits in the image with a caption that takes longer', async () => {
    createRuntime();
    await (await fake('configure', { responseDelay: 3_500 })).text();
    await (await control('start')).text();
    await connected();
    const interaction = captionInteraction();
    const deadline = Number(BigInt(interaction.d.id) >> 22n) + 1_420_070_400_000 + 3_000;
    await (await fake('emit', interaction)).text();
    await vi.waitFor(async () => expect((await discordState()).edits).toHaveLength(1), {
      timeout: 8_000,
    });

    const state = await discordState();
    expect(state.callbacks).toEqual([{ type: 5 }]);
    expect(state.callbackTimes[0]).toBeLessThan(deadline);
    expect(state.edits).toEqual([
      {
        method: 'PATCH',
        path: '/api/v10/webhooks/900/interaction-token/messages/@original',
        body: {
          content: 'Hey!',
          allowed_mentions: { parse: [] },
          attachments: [{ id: 0, filename: 'cat.png' }],
        },
        files: [
          { name: 'files[0]', filename: 'cat.png', type: 'image/png', bytes: [137, 80, 78, 71] },
        ],
        at: expect.any(Number),
      },
    ]);
    expect(state.edits[0]!.at).toBeGreaterThan(deadline);
  }, 15_000);

  it('replies once to each mention and ignores other messages and replayed duplicates', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('emit', message('499', 2, []))).text();
    await (await fake('emit', message('500', 3))).text();
    await (await fake('emit', message('500', 4))).text();
    await (await fake('emit', message('501', 5))).text();
    await vi.waitFor(async () => expect((await discordState()).replies).toHaveLength(2));

    const state = await discordState();
    expect(state.replies.map((reply) => reply.content)).toEqual(['Hey!', 'Hey!']);
    expect(state.replies.map((reply) => reply.message_reference.message_id).sort()).toEqual([
      '500',
      '501',
    ]);
  }, 15_000);

  it('pauses fatal intent failures instead of reconnecting on every cron tick', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('disconnect', { code: 4014 })).text();
    await vi.waitFor(async () => expect((await status()).error).toContain('4014'));
    await reconcile();
    await reconcile();

    expect((await status()).running).toBe(false);
    expect((await discordState()).discoveries).toBe(1);
  }, 15_000);

  it('does not identify when the session-start budget is exhausted', async () => {
    createRuntime();
    await (await fake('configure', { remaining: 0 })).text();
    await (await control('start')).text();
    await vi.waitFor(async () => expect((await status()).retryAt).not.toBeNull());
    await reconcile();

    expect((await discordState()).packets).toHaveLength(0);
    expect((await discordState()).discoveries).toBe(1);
    expect((await status()).error).toContain('allowance');
  });

  it('recovers an ordinary disconnect through the alarm without re-identifying', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('disconnect', { code: 4000 })).text();
    await vi.waitFor(
      async () =>
        expect((await discordState()).packets.some((packet) => packet.op === 6)).toBe(true),
      { timeout: 8_000 },
    );
    await connected();

    expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(1);
  }, 15_000);

  it('recovers through the watchdog when a closed socket never reports its close', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await runtime!.dispatchFetch('https://bot.test/_test/zombie')).text();
    await reconcile();
    await vi.waitFor(
      async () =>
        expect((await discordState()).packets.some((packet) => packet.op === 6)).toBe(true),
      { timeout: 8_000 },
    );
    await connected();

    expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(1);
  }, 15_000);

  it('invalidates a non-resumable session and respects the persisted Identify cooldown', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('emit', { op: 9, d: false })).text();
    await vi.waitFor(
      async () =>
        expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(2),
      { timeout: 12_000 },
    );
    await connected();

    expect((await discordState()).packets.some((packet) => packet.op === 6)).toBe(false);
  }, 15_000);

  it('falls back to a fresh session when the resume endpoint stays broken, even across a DO reset', async () => {
    createRuntime('prod');
    await reconcile();
    await connected();
    await (await fake('configure', { resumeStatus: 503 })).text();
    await (await fake('emit', { op: 7 })).text();
    await vi.waitFor(
      async () => {
        const resumes = (await discordState()).urls.filter(
          (url) => new URL(url).hostname === 'resume.discord.gg',
        );
        expect(resumes).toHaveLength(1);
        expect((await status()).retryAt).not.toBeNull();
      },
      { timeout: 8_000 },
    );
    await (await runtime!.dispatchFetch('https://bot.test/_test/crash')).text();
    await reconcile();
    await vi.waitFor(
      async () => {
        expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(2);
      },
      { timeout: 20_000 },
    );
    await connected();

    const state = await discordState();
    expect(state.urls.filter((url) => new URL(url).hostname === 'resume.discord.gg')).toHaveLength(
      2,
    );
    expect(new URL(state.urls.at(-1)!).hostname).toBe('gateway.discord.gg');
    expect((await status()).error).toBeNull();
  }, 30_000);

  it('stops an in-flight handshake without letting it reconnect later', async () => {
    createRuntime();
    await (await fake('configure', { holdReady: true })).text();
    await (await control('start')).text();
    await vi.waitFor(async () =>
      expect((await discordState()).packets.some((packet) => packet.op === 2)).toBe(true),
    );
    await (await control('stop')).text();
    await (await fake('configure', { holdReady: false })).text();
    await reconcile();

    await vi.waitFor(async () => expect((await discordState()).connections).toBe(0));
    expect(await status()).toMatchObject({ running: false, connected: false });
    expect((await discordState()).discoveries).toBe(1);
  });

  it('preserves the session when Discord requests a reconnect', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('emit', { op: 7 })).text();
    await vi.waitFor(async () => expect((await status()).lastEvent).toBe('RESUMED'), {
      timeout: 8_000,
    });

    const state = await discordState();
    expect(state.packets.filter((packet) => packet.op === 2)).toHaveLength(1);
    expect(state.closeCodes).toContain(3000);
    expect(state.closeCodes).not.toContain(1000);
  }, 15_000);

  it('starts the Identify cooldown at transmission, even after a slow handshake', async () => {
    createRuntime();
    await (await fake('configure', { openDelay: 6_100 })).text();
    await (await control('start')).text();
    await connected();
    await (await fake('configure', { openDelay: 0 })).text();
    await (await fake('disconnect', { code: 4007 })).text();
    await vi.waitFor(
      async () =>
        expect((await discordState()).packets.filter((packet) => packet.op === 2)).toHaveLength(2),
      { timeout: 12_000 },
    );

    const identifies = (await discordState()).packets.filter((packet) => packet.op === 2);
    expect(identifies[1]!.receivedAt - identifies[0]!.receivedAt).toBeGreaterThanOrEqual(5_000);
  }, 25_000);

  it('skips expired interactions without paying for an OpenAI request or attempting a callback', async () => {
    createRuntime();
    await (await control('start')).text();
    await connected();
    await (await fake('emit', conchInteraction(10_000))).text();
    await vi.waitFor(async () => expect((await status()).lastEvent).toBe('INTERACTION_CREATE'));

    const state = await discordState();
    expect(state.decisions).toBe(0);
    expect(state.callbacks).toHaveLength(0);
  });

  it('shortens generation for delayed interactions to leave time for the direct callback', async () => {
    createRuntime();
    await (await fake('configure', { decisionDelay: 2_000 })).text();
    await (await control('start')).text();
    await connected();
    const interaction = conchInteraction(1_000);
    const deadline = Number(BigInt(interaction.d.id) >> 22n) + 1_420_070_400_000 + 3_000;
    await (await fake('emit', interaction)).text();
    await vi.waitFor(async () => expect((await discordState()).callbacks).toHaveLength(1), {
      timeout: 4_000,
    });

    const state = await discordState();
    expect(state.decisions).toBe(1);
    expect(state.callbackTimes[0]).toBeLessThan(deadline);
    expect(state.callbacks[0]!.data.content).toBe("The conch couldn't answer. Try again.");
    expect(state.callbacks[0]!.type).toBe(4);
  }, 10_000);

  it('ignores the result of Gateway discovery after that generation is stopped', async () => {
    createRuntime();
    await (await fake('configure', { holdDiscovery: true, discoveryStatus: 401 })).text();
    await (await control('start')).text();
    await vi.waitFor(async () => expect((await discordState()).discoveries).toBe(1));
    await (await control('stop')).text();
    await (await fake('release', {})).text();
    await reconcile();

    expect(await status()).toMatchObject({ running: false, connected: false, error: null });
    expect((await discordState()).connections).toBe(0);
  });
});
