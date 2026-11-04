import type { APIUser } from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { GatewayConnection, type Session } from '../src/gateway/connection.ts';

class FakeSocket extends EventTarget {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSING = 2;
  static CLOSED = 3;
  static latest: FakeSocket;
  readyState = FakeSocket.OPEN;
  sent: unknown[] = [];
  closedWith: number | undefined;
  readonly url: string;

  constructor(url: string) {
    super();
    this.url = url;
    FakeSocket.latest = this;
  }

  send(data: string) {
    this.sent.push(JSON.parse(data));
  }

  close(code: number) {
    this.closedWith = code;
    this.readyState = FakeSocket.CLOSING;
  }

  receive(packet: object) {
    this.dispatchEvent(new MessageEvent('message', { data: JSON.stringify(packet) }));
  }

  closeFromServer(code: number) {
    this.readyState = FakeSocket.CLOSED;
    this.dispatchEvent(new CloseEvent('close', { code }));
  }
}

const HELLO = { op: 10, d: { heartbeat_interval: 45_000 } };
const BOT = { id: '100', username: 'wasabibot' } as APIUser;
const SESSION: Session = {
  sessionId: 'session-1',
  resumeGatewayUrl: 'wss://resume',
  sequence: 5,
  user: BOT,
};

function connect(session: Session | null = null) {
  const callbacks = { onIdentify: vi.fn(), onDispatch: vi.fn(), onClose: vi.fn() };
  const connection = new GatewayConnection({
    token: 'bot-token',
    url: 'wss://gateway.discord.gg',
    session,
    ...callbacks,
  });
  return { connection, socket: FakeSocket.latest, ...callbacks };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(Math, 'random').mockReturnValue(0.5);
  vi.stubGlobal('WebSocket', FakeSocket);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('GatewayConnection', () => {
  it('identifies after HELLO and tracks the new session', () => {
    const { connection, socket, onIdentify, onDispatch } = connect();
    expect(socket.url).toBe('wss://gateway.discord.gg/?v=10&encoding=json');

    socket.receive(HELLO);
    socket.receive({
      op: 0,
      t: 'READY',
      s: 1,
      d: { session_id: 'session-1', resume_gateway_url: 'wss://resume', user: BOT },
    });
    socket.receive({ op: 0, t: 'MESSAGE_CREATE', s: 2, d: {} });

    expect(onIdentify).toHaveBeenCalledOnce();
    expect(socket.sent).toEqual([
      { op: 2, d: expect.objectContaining({ token: 'bot-token', intents: expect.any(Number) }) },
    ]);
    expect(connection.connected).toBe(true);
    expect(connection.session).toEqual({ ...SESSION, sequence: 2 });
    expect(onDispatch).toHaveBeenCalledTimes(2);
  });

  it('resumes a saved session without identifying', () => {
    const { socket, onIdentify } = connect(SESSION);

    socket.receive(HELLO);

    expect(onIdentify).not.toHaveBeenCalled();
    expect(socket.sent).toEqual([
      { op: 6, d: { token: 'bot-token', session_id: 'session-1', seq: 5 } },
    ]);
  });

  it('sends voice state updates', () => {
    const { connection, socket } = connect(SESSION);
    socket.receive(HELLO);
    socket.sent = [];
    const data = { guild_id: '300', channel_id: '400', self_mute: false, self_deaf: true };

    connection.updateVoiceState(data);

    expect(socket.sent).toEqual([{ op: 4, d: data }]);
  });

  it('heartbeats with the last sequence and treats a missed ACK as a zombie', () => {
    const { connection, socket, onClose } = connect(SESSION);
    socket.receive(HELLO);
    socket.receive({ op: 0, t: 'RESUMED', s: 6, d: {} });
    socket.sent = [];

    vi.advanceTimersByTime(22_500);
    socket.receive({ op: 11 });
    socket.receive({ op: 1 });
    vi.advanceTimersByTime(45_000);
    expect(socket.sent).toEqual([
      { op: 1, d: 6 },
      { op: 1, d: 6 },
      { op: 1, d: 6 },
    ]);
    expect(connection.lastAck).toBe(Date.now() - 45_000);
    expect(onClose).not.toHaveBeenCalled();

    vi.advanceTimersByTime(45_000);

    expect(onClose).toHaveBeenCalledExactlyOnceWith(4000, true, 'heartbeat not acknowledged');
    expect(socket.closedWith).toBe(3000);
    expect(connection.alive).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each([
    [{ op: 7 }, true, 3000, 'Discord asked to reconnect'],
    [{ op: 9, d: true }, true, 3000, 'invalid session'],
    [{ op: 9, d: false }, false, 1000, 'invalid session'],
  ] as const)('hands %j back to the caller', (packet, resumable, closedWith, reason) => {
    const { socket, onClose } = connect();

    socket.receive(packet);

    expect(onClose).toHaveBeenCalledExactlyOnceWith(4000, resumable, reason);
    expect(socket.closedWith).toBe(closedWith);
  });

  it.each([
    [1006, true],
    [4000, true],
    [1000, false],
    [4007, false],
  ])('reports a server close with code %s as resumable=%s', (code, resumable) => {
    const { socket, onClose } = connect();

    socket.closeFromServer(code);

    expect(onClose).toHaveBeenCalledExactlyOnceWith(code, resumable, 'socket closed');
  });

  it('gives up on a handshake that never completes', () => {
    const { onClose } = connect();

    vi.advanceTimersByTime(10_000);

    expect(onClose).toHaveBeenCalledExactlyOnceWith(4000, true, 'handshake timed out');
  });

  it('goes quiet after close()', () => {
    const { connection, socket, onClose, onDispatch } = connect();
    socket.receive(HELLO);

    connection.close();
    socket.receive({ op: 0, t: 'MESSAGE_CREATE', s: 2, d: {} });
    socket.closeFromServer(1006);
    vi.advanceTimersByTime(120_000);

    expect(socket.closedWith).toBe(1000);
    expect(onClose).not.toHaveBeenCalled();
    expect(onDispatch).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('closes a socket that finishes opening after close()', () => {
    const { connection, socket } = connect();
    socket.readyState = FakeSocket.CONNECTING;

    connection.close();
    expect(socket.closedWith).toBeUndefined();
    socket.readyState = FakeSocket.OPEN;
    socket.dispatchEvent(new Event('open'));

    expect(socket.closedWith).toBe(1000);
  });
});
