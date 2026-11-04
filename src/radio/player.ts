import { DurableObject, tracing, WorkerEntrypoint } from 'cloudflare:workers';
import {
  GatewayDispatchEvents,
  GatewayOpcodes,
  type GatewayVoiceServerUpdateDispatch,
  type GatewayVoiceStateUpdate,
  type GatewayVoiceStateUpdateDispatch,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { log } from '../log.ts';
import { gateway } from '../gateway/stub.ts';

const PORT = 8080;
const CALLBACK_HOST = 'player.internal';
const WATCHDOG_MS = 30_000;
const START_TIMEOUT_MS = 30_000;
const REQUEST_TIMEOUT_MS = 5_000;
const PLAY_TIMEOUT_MS = 60_000;
const INACTIVITY_TIMEOUT_MS = 2 * 60 * 1_000;
const MAX_SESSION_MS = 12 * 60 * 60 * 1_000;
const START_FAILED =
  "The radio couldn't start. If the bot was just updated, try again in a minute.";
const PLAY_FAILED = "The radio couldn't play that station.";

export interface Station {
  name: string;
  url: string;
}

interface Session {
  guildId: string;
  channelId: string;
  startedAt: number;
}

export class RadioPlayer extends DurableObject<Env> {
  private booting: Promise<void> | undefined;

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    // A deploy restarts the Durable Object but leaves its container running.
    if (ctx.container?.running) void ctx.blockConcurrencyWhile(() => this.watch());
  }

  async play(guildId: string, channelId: string, station: Station): Promise<string | undefined> {
    try {
      await this.boot(guildId);
    } catch (error) {
      log.error('Radio container failed to start', { guildId, error });
      await this.shutDown(guildId);
      return START_FAILED;
    }
    const response = await this.request(
      '/play',
      { guildId, channelId, url: station.url },
      PLAY_TIMEOUT_MS,
    ).catch((error: unknown) => {
      log.error('Radio play request failed', { guildId, error });
      return undefined;
    });
    const reason = response?.ok ? (await response.json<{ error?: string }>()).error : PLAY_FAILED;
    if (reason) {
      await this.shutDown(guildId);
      return reason;
    }
    this.save({ guildId, channelId, startedAt: this.session?.startedAt ?? Date.now() });
    await this.ctx.storage.setAlarm(Date.now() + WATCHDOG_MS);
    return undefined;
  }

  async stop() {
    const session = this.session;
    await this.shutDown(session?.guildId);
    return session !== null;
  }

  async voiceEvent(packet: GatewayVoiceStateUpdateDispatch | GatewayVoiceServerUpdateDispatch) {
    if (!this.ctx.container?.running) return;
    const session = this.session;
    if (session && packet.t === GatewayDispatchEvents.VoiceStateUpdate && packet.d.channel_id) {
      this.save({ ...session, channelId: packet.d.channel_id });
    }
    await this.request('/voice', packet).catch((error: unknown) =>
      log.error('Radio voice event failed', { guildId: packet.d.guild_id, error }),
    );
  }

  // Alarms stop retrying after six failures, so re-arm here instead of letting the loop die.
  override async alarm() {
    try {
      await this.check();
    } catch (error) {
      log.error('Radio watchdog failed', { guildId: this.session?.guildId, error });
      if (this.session) await this.ctx.storage.setAlarm(Date.now() + WATCHDOG_MS);
    }
  }

  private async check() {
    const session = this.session;
    if (!session || !this.ctx.container?.running) {
      await this.stop();
      return;
    }
    const status = await this.request('/status').then(
      (response) => response.json<{ active: boolean }>(),
      () => undefined,
    );
    const listeners = await gateway(this.env).listeners(session.guildId, session.channelId);
    const now = Date.now();
    if (!status?.active || listeners === 0 || now - session.startedAt >= MAX_SESSION_MS) {
      await this.stop();
      return;
    }
    await this.ctx.storage.setAlarm(now + WATCHDOG_MS);
  }

  // Plays that arrive together share one start, since a second start() would throw.
  private boot(guildId: string) {
    this.booting ??= this.startContainer(guildId).finally(() => {
      this.booting = undefined;
    });
    return this.booting;
  }

  private async startContainer(guildId: string) {
    if (this.ctx.container!.running) return;
    await tracing.enterSpan('radio.boot', () => this.launch(guildId));
  }

  private async launch(guildId: string) {
    const container = this.ctx.container!;
    // Intercepts last until the container stops, so each new container gets its own.
    await container.interceptOutboundHttp(
      CALLBACK_HOST,
      this.ctx.exports.RadioCallback({ props: { guildId } }),
    );
    container.start({ enableInternet: true });
    await this.watch();
    const deadline = Date.now() + START_TIMEOUT_MS;
    while (
      !(await this.request('/status').then(
        (response) => response.ok,
        () => false,
      ))
    ) {
      if (!container.running || Date.now() > deadline) {
        throw new Error('The container did not become healthy.');
      }
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  }

  // Takes the guild rather than reading the session, since a failed start may have joined
  // the channel without one.
  private async shutDown(guildId: string | undefined) {
    // Forget the session first, so the container's exit doesn't leave a second time.
    this.ctx.storage.kv.delete('session');
    await this.ctx.storage.deleteAlarm();
    const container = this.ctx.container!;
    if (container.running) await container.destroy();
    if (guildId) await this.leaveVoice(guildId);
  }

  private async watch() {
    const container = this.ctx.container!;
    await container.setInactivityTimeout(INACTIVITY_TIMEOUT_MS);
    this.ctx.waitUntil(
      container.monitor().then(
        () => this.containerExited('exited'),
        (error: unknown) => this.containerExited(error),
      ),
    );
  }

  private async containerExited(reason: unknown) {
    const session = this.session;
    if (!session) return;
    log.error('Radio container stopped unexpectedly', { guildId: session.guildId, error: reason });
    await this.shutDown(session.guildId);
  }

  private async leaveVoice(guildId: string) {
    await gateway(this.env).updateVoiceState({
      guild_id: guildId,
      channel_id: null,
      self_mute: false,
      self_deaf: false,
    });
  }

  private request(path: string, body?: unknown, timeoutMs = REQUEST_TIMEOUT_MS) {
    return this.ctx.container!.getTcpPort(PORT).fetch(`http://container${path}`, {
      method: body === undefined ? 'GET' : 'POST',
      ...(body !== undefined && {
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
      signal: AbortSignal.timeout(timeoutMs),
    });
  }

  private get session() {
    return this.ctx.storage.kv.get<Session>('session') ?? null;
  }

  private save(session: Session) {
    this.ctx.storage.kv.put('session', session);
  }
}

export class RadioCallback extends WorkerEntrypoint<Env, { guildId: string }> {
  override async fetch(request: Request) {
    if (request.method !== 'POST' || new URL(request.url).pathname !== '/gateway') {
      return new Response('Not found', { status: 404 });
    }
    const payload = await request.json<GatewayVoiceStateUpdate>();
    if (
      payload.op !== GatewayOpcodes.VoiceStateUpdate ||
      payload.d.guild_id !== this.ctx.props.guildId
    ) {
      return new Response('Forbidden', { status: 403 });
    }
    const sent = await gateway(this.env).updateVoiceState(payload.d);
    return new Response(null, { status: sent ? 204 : 503 });
  }
}
