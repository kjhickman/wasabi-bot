import { DurableObject } from 'cloudflare:workers';
import {
  GatewayDispatchEvents,
  Routes,
  type GatewayVoiceStateUpdateData,
  type RESTGetAPIGatewayBotResult,
  type RESTGetCurrentApplicationResult,
} from 'discord-api-types/v10';
import { commands, messageCommands } from '../commands/index.ts';
import { discordFetch } from '../discord.ts';
import type { Env } from '../env.ts';
import { log } from '../log.ts';
import { migrate, prune } from '../storage.ts';
import {
  FATAL_CLOSE_CODES,
  GatewayConnection,
  RESUMABLE_CLOSE_CODE,
  type Session,
} from './connection.ts';
import { handleEvent } from './events.ts';
import { GATEWAY_NAME } from './stub.ts';
import { countListeners } from './voice-states.ts';

const WATCHDOG_MS = 30_000;
const MAX_BACKOFF_MS = 5 * 60 * 1_000;
const DISCOVERY_TIMEOUT_MS = 5_000;
const COMMAND_SYNC_TIMEOUT_MS = 10_000;
const MIN_RATE_LIMIT_WAIT_MS = 60_000;
const SESSION_START_RESERVE = 5;
const FAILURES_BEFORE_NEW_SESSION = 3;
const COMMANDS = JSON.stringify(
  [...commands, ...messageCommands].map((command) => command.definition),
);

interface Lifecycle {
  enabled: boolean;
  session: Session | null;
  retryAt: number;
  failures: number;
  identifyAfter: number;
  error: string | null;
}

export class DiscordGateway extends DurableObject<Env> {
  protected readonly identifyCooldownMs: number = 6_000;
  protected readonly retryBaseMs: number = 1_000;
  private lifecycle: Lifecycle;
  protected connection: GatewayConnection | undefined;
  private connecting: Promise<void> | undefined;
  private generation = 0;
  private lastEvent: string | null = null;
  private commandsError: string | null = null;

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
      commandsError: this.commandsError,
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

  updateVoiceState(data: GatewayVoiceStateUpdateData) {
    if (!this.connection?.connected) return false;
    this.connection.updateVoiceState(data);
    return true;
  }

  listeners(guildId: string, channelId: string) {
    const botId = this.lifecycle.session?.user.id ?? '';
    return countListeners(this.ctx.storage.sql, guildId, channelId, botId);
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
    this.ctx.waitUntil(this.syncCommands(token));
    if (this.connection && !this.connection.alive) {
      log.warn('Gateway connection died without closing');
      this.retry();
    }
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

  // Alarms stop retrying after six failures, so re-arm here instead of letting the loop die.
  override async alarm() {
    try {
      prune(this.ctx.storage.sql);
      await this.ensureConnected();
    } catch (error) {
      log.error('Gateway alarm failed', { error });
      if (this.shouldRun()) await this.armAlarm();
    }
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
    if (error) log.error('Gateway halted', { reason: error });
    else log.info('Gateway stopped');
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

  private async syncCommands(token: string) {
    if (this.ctx.storage.kv.get('commands') === COMMANDS) return;
    try {
      const signal = AbortSignal.timeout(COMMAND_SYNC_TIMEOUT_MS);
      const application = await discordFetch(Routes.currentApplication(), { token, signal });
      if (!application.ok) throw new Error(`HTTP ${application.status}`);
      const { id } = await application.json<RESTGetCurrentApplicationResult>();
      const response = await discordFetch(Routes.applicationCommands(id), {
        token,
        signal,
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: COMMANDS,
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}: ${await response.text()}`);
      this.ctx.storage.kv.put('commands', COMMANDS);
      this.commandsError = null;
    } catch (error) {
      this.commandsError = `Command sync failed: ${error instanceof Error ? error.message : String(error)}`;
      log.error('Command sync failed', { error });
    }
  }

  private async armAlarm() {
    const current = await this.ctx.storage.getAlarm();
    const { retryAt } = this.lifecycle;
    let next = Date.now() + WATCHDOG_MS;
    if (retryAt > Date.now()) next = Math.min(next, retryAt);
    if (current === null || current > next) await this.ctx.storage.setAlarm(next);
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
      if (!response.ok) {
        log.warn('Gateway discovery failed', { status: response.status });
        return this.retry();
      }

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
        log.warn('Gateway is out of session starts', { ...gateway.session_start_limit });
        return this.retry();
      }
      if (!session && identifyAfter > Date.now()) {
        this.lifecycle.retryAt = identifyAfter;
        return this.retry();
      }
      this.connection = this.open(token, session?.resumeGatewayUrl ?? gateway.url, session);
    } catch (error) {
      if (stale()) return;
      log.warn('Gateway connection attempt failed', { error });
      this.retry();
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
          log.info(packet.t === GatewayDispatchEvents.Ready ? 'Gateway ready' : 'Gateway resumed', {
            afterFailures: this.lifecycle.failures,
          });
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
      onClose: (code, resumable, reason) => {
        if (!this.shouldRun()) return;
        if (FATAL_CLOSE_CODES.has(code)) {
          void this.halt(
            `Discord closed the Gateway (${code}); check the bot token, intents, and shard configuration, then start it again.`,
          );
          return;
        }
        if (!resumable) this.lifecycle.session = null;
        this.retry();
        const { failures, retryAt } = this.lifecycle;
        log.warn('Gateway disconnected', { code, reason, resumable, failures, retryAt });
      },
    });
    return connection;
  }
}
