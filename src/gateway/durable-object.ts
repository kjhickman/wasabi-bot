import { DurableObject } from 'cloudflare:workers';
import {
  GatewayDispatchEvents,
  Routes,
  type RESTGetAPIGatewayBotResult,
} from 'discord-api-types/v10';
import { discordFetch } from '../discord.ts';
import type { Env } from '../env.ts';
import { migrate, prune } from '../storage.ts';
import {
  FATAL_CLOSE_CODES,
  GatewayConnection,
  RESUMABLE_CLOSE_CODE,
  type Session,
} from './connection.ts';
import { handleEvent } from './events.ts';
import { GATEWAY_NAME } from './stub.ts';

const WATCHDOG_MS = 30_000;
const MAX_BACKOFF_MS = 5 * 60 * 1_000;
const DISCOVERY_TIMEOUT_MS = 5_000;
const MIN_RATE_LIMIT_WAIT_MS = 60_000;
const SESSION_START_RESERVE = 5;
// The first disconnect plus two failed resumes: start a fresh session instead.
const FAILURES_BEFORE_NEW_SESSION = 3;

interface Lifecycle {
  enabled: boolean;
  session: Session | null;
  retryAt: number;
  failures: number;
  identifyAfter: number;
  error: string | null;
}

// Decides whether the single Gateway connection should run, and when to (re)connect.
// Durable alarms drive every reconnect so recovery survives Durable Object restarts.
export class DiscordGateway extends DurableObject<Env> {
  protected readonly identifyCooldownMs: number = 6_000;
  protected readonly retryBaseMs: number = 1_000;
  private lifecycle: Lifecycle;
  protected connection: GatewayConnection | undefined;
  private connecting: Promise<void> | undefined;
  private generation = 0;
  private lastEvent: string | null = null;

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    this.lifecycle = ctx.storage.kv.get<Lifecycle>('lifecycle') ?? {
      enabled: env.GATEWAY_MODE === 'prod',
      session: null,
      retryAt: 0,
      failures: 0,
      identifyAfter: 0,
      error: null,
    };
    // Sessions saved before they held the bot user can't tell chat who the bot is.
    if (this.lifecycle.session && !this.lifecycle.session.user) this.lifecycle.session = null;
    migrate(ctx.storage.sql);
  }

  status() {
    const { retryAt, error } = this.lifecycle;
    return {
      mode: this.env.GATEWAY_MODE,
      running: this.shouldRun(),
      connected: this.connection?.connected ?? false,
      retryAt: retryAt > Date.now() ? new Date(retryAt).toISOString() : null,
      lastHeartbeatAck: this.connection?.lastAck ?? null,
      lastEvent: this.lastEvent,
      error,
    };
  }

  async start() {
    this.update({ enabled: true, retryAt: 0, failures: 0, error: null });
    return this.ensureConnected();
  }

  async stop() {
    await this.halt(null);
    return this.status();
  }

  async ensureConnected() {
    // An object left under an old name keeps waking on its alarm and would open a
    // second Gateway connection, so it erases itself instead.
    if (this.ctx.id.name !== GATEWAY_NAME) {
      await this.ctx.storage.deleteAll();
      return this.status();
    }
    if (!this.shouldRun()) return this.status();
    const token = this.env.DISCORD_BOT_TOKEN;
    if (!token || !this.env.GATEWAY_CONTROL_TOKEN) {
      await this.halt(
        'Set DISCORD_BOT_TOKEN and GATEWAY_CONTROL_TOKEN before starting the Gateway.',
      );
      return this.status();
    }
    this.save();
    await this.armAlarm();
    if (!this.shouldRun()) return this.status();
    if (this.connection && !this.connection.alive) this.retry();
    if (this.connecting || this.connection || this.lifecycle.retryAt > Date.now()) {
      return this.status();
    }
    const attempt = this.attemptConnection(token).finally(() => {
      if (this.connecting === attempt) this.connecting = undefined;
    });
    this.connecting = attempt;
    this.ctx.waitUntil(attempt);
    return this.status();
  }

  override async alarm() {
    prune(this.ctx.storage.sql);
    await this.ensureConnected();
  }

  private shouldRun() {
    return this.lifecycle.enabled;
  }

  private save() {
    this.ctx.storage.kv.put('lifecycle', this.lifecycle);
  }

  private update(changes: Partial<Lifecycle>) {
    Object.assign(this.lifecycle, changes);
    this.save();
  }

  private async halt(error: string | null) {
    this.update({ enabled: false, session: null, retryAt: 0, error });
    this.disconnect();
    await this.ctx.storage.deleteAlarm();
  }

  private disconnect(closeCode?: number) {
    this.generation++;
    this.connecting = undefined;
    this.connection?.close(closeCode);
    this.connection = undefined;
  }

  private retry() {
    this.disconnect(RESUMABLE_CLOSE_CODE);
    const failures = ++this.lifecycle.failures;
    if (failures >= FAILURES_BEFORE_NEW_SESSION) this.lifecycle.session = null;
    const backoff = Math.min(MAX_BACKOFF_MS, this.retryBaseMs * 2 ** failures);
    this.lifecycle.retryAt = Math.max(this.lifecycle.retryAt, Date.now() + backoff);
    this.lifecycle.error ??= 'Gateway disconnected; reconnect scheduled.';
    this.save();
    this.ctx.waitUntil(this.armAlarm());
  }

  private async armAlarm() {
    const { retryAt } = this.lifecycle;
    let next = Date.now() + WATCHDOG_MS;
    if (retryAt > Date.now()) next = Math.min(next, retryAt);
    await this.ctx.storage.setAlarm(next);
  }

  private async attemptConnection(token: string) {
    const generation = this.generation;
    const stale = () => generation !== this.generation || !this.shouldRun();
    try {
      const response = await discordFetch(Routes.gatewayBot(), {
        token,
        signal: AbortSignal.timeout(DISCOVERY_TIMEOUT_MS),
      });
      if (stale()) return;
      if (response.status === 401 || response.status === 403) {
        return await this.halt(
          `Discord rejected Gateway discovery (${response.status}); check DISCORD_BOT_TOKEN.`,
        );
      }
      if (response.status === 429) {
        const retryAfter = Number(response.headers.get('Retry-After'));
        this.lifecycle.retryAt =
          Date.now() +
          Math.max(MIN_RATE_LIMIT_WAIT_MS, Number.isFinite(retryAfter) ? retryAfter * 1_000 : 0);
      }
      if (!response.ok) return this.retry();

      const gateway = await response.json<RESTGetAPIGatewayBotResult>();
      if (stale()) return;
      if (gateway.shards > 1) {
        return await this.halt(
          'Discord recommends multiple shards; this deployment supports one shard.',
        );
      }
      const { session, identifyAfter } = this.lifecycle;
      if (!session && gateway.session_start_limit.remaining <= SESSION_START_RESERVE) {
        this.lifecycle.retryAt = Date.now() + gateway.session_start_limit.reset_after;
        this.lifecycle.error = 'Waiting for the Discord session-start allowance to reset.';
        return this.retry();
      }
      if (!session && identifyAfter > Date.now()) {
        this.lifecycle.retryAt = identifyAfter;
        return this.retry();
      }
      this.connection = this.open(token, session?.resumeGatewayUrl ?? gateway.url, session);
    } catch {
      if (!stale()) this.retry();
    }
  }

  private open(token: string, url: string, session: Session | null) {
    const connection: GatewayConnection = new GatewayConnection({
      token,
      url,
      session,
      onIdentify: () => {
        this.update({ identifyAfter: Date.now() + this.identifyCooldownMs });
      },
      onDispatch: (packet) => {
        if (!this.shouldRun()) return;
        const { session } = connection;
        this.lastEvent = packet.t;
        this.lifecycle.session = session;
        if (
          packet.t === GatewayDispatchEvents.Ready ||
          packet.t === GatewayDispatchEvents.Resumed
        ) {
          this.update({ failures: 0, retryAt: 0, error: null });
        }
        if (session) {
          this.ctx.waitUntil(
            handleEvent(packet, {
              bot: session.user,
              token,
              env: this.env,
              sql: this.ctx.storage.sql,
            }),
          );
        }
      },
      onClose: (code, resumable) => {
        if (!this.shouldRun()) return;
        if (FATAL_CLOSE_CODES.has(code)) {
          void this.halt(
            `Discord closed the Gateway (${code}); check the bot token, intents, and shard configuration, then start it again.`,
          );
          return;
        }
        if (!resumable) this.lifecycle.session = null;
        this.retry();
      },
    });
    return connection;
  }
}
