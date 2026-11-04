import { DurableObject, WorkerEntrypoint } from 'cloudflare:workers';
import {
  GatewayDispatchEvents,
  GatewayOpcodes,
  type GatewayVoiceServerUpdateDispatch,
  type GatewayVoiceStateUpdate,
  type GatewayVoiceStateUpdateDispatch,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { errorMessage } from '../errors.ts';
import { gateway } from '../gateway/stub.ts';

const PORT = 8080;
// The container reaches its Durable Object through requests to this host.
const CALLBACK_HOST = 'player.internal';
const WATCHDOG_MS = 30_000;
const START_TIMEOUT_MS = 30_000;
const REQUEST_TIMEOUT_MS = 5_000;
// Joining voice and waiting for the stream's first audio.
const PLAY_TIMEOUT_MS = 60_000;
// Stops the container if the watchdog alarm ever stops running.
const INACTIVITY_TIMEOUT_MS = 2 * 60 * 1_000;
// A backstop in case the record of who is listening drifts.
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

// One per guild. Owns the container that holds the guild's voice connection and streams
// the station, and leaves once nobody is listening.
export class RadioPlayer extends DurableObject<Env> {
  private booting: Promise<void> | undefined;

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    // A deploy restarts the Durable Object but leaves its container running.
    if (ctx.container?.running) void ctx.blockConcurrencyWhile(() => this.watch());
  }

  // Returns why the radio couldn't play, if it couldn't.
  async play(guildId: string, channelId: string, station: Station): Promise<string | undefined> {
    try {
      await this.boot(guildId);
    } catch (error) {
      console.error('Radio container failed to start', errorMessage(error));
      await this.shutDown(guildId);
      return START_FAILED;
    }
    const response = await this.request(
      '/play',
      { guildId, channelId, url: station.url },
      PLAY_TIMEOUT_MS,
    ).catch((error: unknown) => {
      console.error('Radio play request failed', errorMessage(error));
      return undefined;
    });
    // The container explains failures it expects, like a dead stream.
    const reason = response?.ok ? (await response.json<{ error?: string }>()).error : PLAY_FAILED;
    if (reason) {
      await this.shutDown(guildId);
      return reason;
    }
    this.save({ guildId, channelId, startedAt: this.session?.startedAt ?? Date.now() });
    await this.ctx.storage.setAlarm(Date.now() + WATCHDOG_MS);
    return undefined;
  }

  // Returns whether the radio was playing.
  async stop() {
    const session = this.session;
    await this.shutDown(session?.guildId);
    return session !== null;
  }

  async voiceEvent(packet: GatewayVoiceStateUpdateDispatch | GatewayVoiceServerUpdateDispatch) {
    if (!this.ctx.container?.running) return;
    const session = this.session;
    // Someone may have dragged the bot to another channel.
    if (session && packet.t === GatewayDispatchEvents.VoiceStateUpdate && packet.d.channel_id) {
      this.save({ ...session, channelId: packet.d.channel_id });
    }
    await this.request('/voice', packet).catch((error: unknown) =>
      console.error('Radio voice event failed', errorMessage(error)),
    );
  }

  // Alarms stop retrying after six failures, so re-arm here instead of letting the loop die.
  override async alarm() {
    try {
      await this.check();
    } catch (error) {
      console.error('Radio watchdog failed', errorMessage(error));
      if (this.session) await this.ctx.storage.setAlarm(Date.now() + WATCHDOG_MS);
    }
  }

  private async check() {
    const session = this.session;
    if (!session || !this.ctx.container?.running) {
      await this.stop();
      return;
    }
    // The container gives up on its own when it loses the voice connection or the stream.
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
    const container = this.ctx.container!;
    if (container.running) return;
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

  // Stops the container and leaves the guild's voice channel. Takes the guild rather than
  // reading the session, since a failed start may have joined the channel without one.
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
        (error: unknown) => this.containerExited(errorMessage(error)),
      ),
    );
  }

  // Shutting down forgets the session first, so a session here means the container stopped
  // on its own, maybe without leaving its voice channel.
  private async containerExited(reason: unknown) {
    const session = this.session;
    if (!session) return;
    console.error('Radio container stopped unexpectedly', reason);
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

// Handles the requests a container makes to its callback host. Its props name the
// container's guild, so a container can't change the bot's voice state anywhere else.
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
