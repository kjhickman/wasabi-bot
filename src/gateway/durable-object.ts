import { DurableObject } from 'cloudflare:workers';
import { GatewayDispatchEvents, type RESTGetAPIGatewayBotResult } from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { FATAL_CLOSE_CODES, GatewayConnection, type Session } from './connection.ts';
import { createInteractionsTable, handleInteraction, pruneInteractions } from './interactions.ts';

const WATCHDOG_MS = 30_000;
const DEV_LEASE_MS = 60 * 60 * 1_000;
const IDENTIFY_COOLDOWN_MS = 6_000;
const MAX_BACKOFF_MS = 5 * 60 * 1_000;

interface Lifecycle {
  enabled: boolean;
  expiresAt: number | null;
  session: Session | null;
  retryAt: number;
  failures: number;
  identifyAfter: number;
  error: string | null;
}

// Decides whether the single Gateway connection should run, and when to (re)connect.
// Durable alarms drive every reconnect so recovery survives Durable Object restarts.
export class DiscordGateway extends DurableObject<Env> {
  private lifecycle: Lifecycle;
  private connection: GatewayConnection | undefined;
  private connecting: Promise<void> | undefined;
  private generation = 0;
  private lastEvent: string | null = null;

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    this.lifecycle = ctx.storage.kv.get<Lifecycle>('lifecycle') ?? {
      enabled: env.GATEWAY_MODE === 'prod',
      expiresAt: null,
      session: null,
      retryAt: 0,
      failures: 0,
      identifyAfter: 0,
      error: null,
    };
    createInteractionsTable(ctx.storage.sql);
  }

  status() {
    const { expiresAt, retryAt, error } = this.lifecycle;
    return {
      mode: this.env.GATEWAY_MODE,
      running: this.shouldRun(),
      connected: this.connection?.connected ?? false,
      leaseExpiresAt: expiresAt === null ? null : new Date(expiresAt).toISOString(),
      retryAt: retryAt > Date.now() ? new Date(retryAt).toISOString() : null,
      lastHeartbeatAck: this.connection?.lastAck ?? null,
      lastEvent: this.lastEvent,
      error,
    };
  }

  async start() {
    Object.assign(this.lifecycle, {
      enabled: true,
      expiresAt: this.env.GATEWAY_MODE === 'prod' ? null : Date.now() + DEV_LEASE_MS,
      retryAt: 0,
      failures: 0,
      error: null,
    });
    this.save();
    return this.ensureConnected();
  }

  async stop() {
    await this.halt(null);
    return this.status();
  }

  async ensureConnected() {
    const { expiresAt } = this.lifecycle;
    if (expiresAt !== null && expiresAt <= Date.now()) return this.stop();
    if (!this.shouldRun()) return this.status();
    const token = this.env.DISCORD_BOT_TOKEN;
    if (!token || !this.env.GATEWAY_CONTROL_TOKEN) {
      await this.halt(
        'Set DISCORD_BOT_TOKEN and GATEWAY_CONTROL_TOKEN before starting the Gateway.',
      );
      return this.status();
    }
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
    pruneInteractions(this.ctx.storage.sql);
    await this.ensureConnected();
  }

  private shouldRun() {
    const { enabled, expiresAt } = this.lifecycle;
    return enabled && (expiresAt === null || expiresAt > Date.now());
  }

  private save() {
    this.ctx.storage.kv.put('lifecycle', this.lifecycle);
  }

  private async halt(error: string | null) {
    Object.assign(this.lifecycle, {
      enabled: false,
      expiresAt: null,
      session: null,
      retryAt: 0,
      error,
    });
    this.save();
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
    this.disconnect(3000);
    const failures = ++this.lifecycle.failures;
    // The first disconnect plus two failed resumes: start a fresh session instead.
    if (failures >= 3) this.lifecycle.session = null;
    const backoff = Math.min(MAX_BACKOFF_MS, 1_000 * 2 ** failures);
    this.lifecycle.retryAt = Math.max(this.lifecycle.retryAt, Date.now() + backoff);
    this.lifecycle.error ??= 'Gateway disconnected; reconnect scheduled.';
    this.save();
    this.ctx.waitUntil(this.armAlarm());
  }

  private async armAlarm() {
    const { retryAt, expiresAt } = this.lifecycle;
    let next = Date.now() + WATCHDOG_MS;
    if (retryAt > Date.now()) next = Math.min(next, retryAt);
    if (expiresAt !== null) next = Math.min(next, expiresAt);
    await this.ctx.storage.setAlarm(next);
  }

  private async attemptConnection(token: string) {
    const generation = this.generation;
    const stale = () => generation !== this.generation || !this.shouldRun();
    try {
      const response = await fetch('https://discord.com/api/v10/gateway/bot', {
        headers: { Authorization: `Bot ${token}` },
        redirect: 'manual',
        signal: AbortSignal.timeout(5_000),
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
          Date.now() + Math.max(60_000, Number.isFinite(retryAfter) ? retryAfter * 1_000 : 0);
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
      if (!session && gateway.session_start_limit.remaining <= 5) {
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
        this.lifecycle.identifyAfter = Date.now() + IDENTIFY_COOLDOWN_MS;
        this.save();
      },
      onDispatch: (packet) => {
        if (!this.shouldRun()) return;
        this.lastEvent = packet.t;
        if (
          packet.t === GatewayDispatchEvents.Ready ||
          packet.t === GatewayDispatchEvents.Resumed
        ) {
          Object.assign(this.lifecycle, { failures: 0, retryAt: 0, error: null });
        }
        this.lifecycle.session = connection.session;
        this.save();
        if (packet.t === GatewayDispatchEvents.InteractionCreate) {
          this.ctx.waitUntil(handleInteraction(packet.d, this.env, this.ctx.storage.sql));
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
