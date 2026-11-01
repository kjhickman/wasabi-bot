import {
  GatewayCloseCodes,
  GatewayDispatchEvents,
  GatewayIntentBits,
  GatewayOpcodes,
  PresenceUpdateStatus,
  type GatewayDispatchPayload,
  type GatewayReceivePayload,
  type GatewaySendPayload,
} from 'discord-api-types/v10';

const HANDSHAKE_TIMEOUT_MS = 10_000;
const INTENTS =
  GatewayIntentBits.Guilds |
  GatewayIntentBits.GuildMembers |
  GatewayIntentBits.GuildMessages |
  GatewayIntentBits.GuildMessageReactions |
  GatewayIntentBits.GuildVoiceStates |
  GatewayIntentBits.DirectMessages |
  GatewayIntentBits.DirectMessageReactions |
  GatewayIntentBits.MessageContent;

export const FATAL_CLOSE_CODES = new Set<number>([
  GatewayCloseCodes.AuthenticationFailed,
  GatewayCloseCodes.InvalidShard,
  GatewayCloseCodes.ShardingRequired,
  GatewayCloseCodes.InvalidAPIVersion,
  GatewayCloseCodes.InvalidIntents,
  GatewayCloseCodes.DisallowedIntents,
]);
const NON_RESUMABLE_CLOSE_CODES = new Set<number>([
  1000,
  1001,
  GatewayCloseCodes.NotAuthenticated,
  GatewayCloseCodes.InvalidSeq,
  GatewayCloseCodes.SessionTimedOut,
]);

export interface Session {
  sessionId: string;
  resumeGatewayUrl: string;
  sequence: number;
}

interface Options {
  token: string;
  url: string;
  session: Session | null;
  onIdentify(): void;
  onDispatch(packet: GatewayDispatchPayload): void;
  onClose(code: number, resumable: boolean): void;
}

// A single Gateway WebSocket. It never reconnects by itself: when it fails it closes,
// reports `onClose` once, and leaves recovery to the Durable Object.
export class GatewayConnection {
  session: Session | null;
  lastAck: number | null = null;
  private ready = false;
  private stopped = false;
  private acked = true;
  private heartbeatTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly handshakeTimer: ReturnType<typeof setTimeout>;
  private readonly socket: WebSocket;
  private readonly options: Options;

  constructor(options: Options) {
    this.options = options;
    this.session = options.session;
    const url = new URL(options.url);
    url.searchParams.set('v', '10');
    url.searchParams.set('encoding', 'json');
    this.socket = new WebSocket(url.href);
    this.socket.addEventListener('message', (event) =>
      this.receive(JSON.parse(event.data as string)),
    );
    this.socket.addEventListener('close', (event) =>
      this.fail(event.code, !NON_RESUMABLE_CLOSE_CODES.has(event.code)),
    );
    this.handshakeTimer = setTimeout(
      () => this.fail(GatewayCloseCodes.UnknownError, true),
      HANDSHAKE_TIMEOUT_MS,
    );
  }

  get alive() {
    return !this.stopped && this.socket.readyState <= WebSocket.OPEN;
  }

  get connected() {
    return this.ready && this.alive;
  }

  // Closing with 1000 ends the Discord session; any other code keeps it resumable.
  close(code = 1000) {
    if (this.stopped) return;
    this.stopped = true;
    clearTimeout(this.handshakeTimer);
    clearTimeout(this.heartbeatTimer);
    if (this.socket.readyState === WebSocket.CONNECTING) {
      this.socket.addEventListener('open', () => this.socket.close(code, 'Gateway stopped'), {
        once: true,
      });
    } else if (this.socket.readyState === WebSocket.OPEN) {
      this.socket.close(code, 'Gateway stopped');
    }
  }

  private fail(code: number, resumable: boolean) {
    if (this.stopped) return;
    this.close(resumable ? 3000 : 1000);
    this.options.onClose(code, resumable);
  }

  private receive(packet: GatewayReceivePayload) {
    if (this.stopped) return;
    switch (packet.op) {
      case GatewayOpcodes.Hello: {
        const interval = packet.d.heartbeat_interval;
        this.scheduleHeartbeat(interval, interval * Math.random());
        if (this.session) this.resume(this.session);
        else this.identify();
        return;
      }
      case GatewayOpcodes.Heartbeat:
        return this.sendHeartbeat();
      case GatewayOpcodes.HeartbeatAck:
        this.acked = true;
        this.lastAck = Date.now();
        return;
      case GatewayOpcodes.Reconnect:
        return this.fail(GatewayCloseCodes.UnknownError, true);
      case GatewayOpcodes.InvalidSession:
        return this.fail(GatewayCloseCodes.UnknownError, packet.d);
      case GatewayOpcodes.Dispatch:
        return this.dispatch(packet);
    }
  }

  private dispatch(packet: GatewayDispatchPayload) {
    if (packet.t === GatewayDispatchEvents.Ready) {
      this.session = {
        sessionId: packet.d.session_id,
        resumeGatewayUrl: packet.d.resume_gateway_url,
        sequence: packet.s,
      };
    } else if (this.session) {
      this.session = { ...this.session, sequence: packet.s };
    }
    if (packet.t === GatewayDispatchEvents.Ready || packet.t === GatewayDispatchEvents.Resumed) {
      this.ready = true;
      clearTimeout(this.handshakeTimer);
    }
    this.options.onDispatch(packet);
  }

  private identify() {
    this.options.onIdentify();
    this.send({
      op: GatewayOpcodes.Identify,
      d: {
        token: this.options.token,
        intents: INTENTS,
        properties: { os: 'linux', browser: 'discord-bot', device: 'Cloudflare Workers' },
        presence: { status: PresenceUpdateStatus.Online, activities: [], afk: false, since: null },
      },
    });
  }

  private resume(session: Session) {
    this.send({
      op: GatewayOpcodes.Resume,
      d: { token: this.options.token, session_id: session.sessionId, seq: session.sequence },
    });
  }

  // An unacknowledged heartbeat means the connection is a zombie.
  private scheduleHeartbeat(interval: number, delay: number) {
    this.heartbeatTimer = setTimeout(() => {
      if (!this.acked) return this.fail(GatewayCloseCodes.UnknownError, true);
      this.acked = false;
      this.sendHeartbeat();
      this.scheduleHeartbeat(interval, interval);
    }, delay);
  }

  private sendHeartbeat() {
    this.send({ op: GatewayOpcodes.Heartbeat, d: this.session?.sequence ?? null });
  }

  private send(payload: GatewaySendPayload) {
    if (this.socket.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(payload));
  }
}
