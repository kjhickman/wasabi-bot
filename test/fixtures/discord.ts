import { DurableObject } from 'cloudflare:workers';

interface Options {
  discoveryStatus: number;
  remaining: number;
  holdReady: boolean;
  holdDiscovery: boolean;
  openDelay: number;
  decisionDelay: number;
  responseDelay: number;
  resumeStatus: number;
}

export class FakeDiscord extends DurableObject {
  private readonly sockets = new Set<WebSocket>();
  private readonly discoveryWaiters: (() => void)[] = [];

  override async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const options = this.ctx.storage.kv.get<Options>('options') ?? {
      discoveryStatus: 200,
      remaining: 1000,
      holdReady: false,
      holdDiscovery: false,
      openDelay: 0,
      decisionDelay: 0,
      responseDelay: 0,
      resumeStatus: 101,
    };
    if (url.pathname === '/configure') {
      this.ctx.storage.kv.put('options', {
        ...options,
        ...(await request.json<Partial<Options>>()),
      });
      return new Response(null, { status: 204 });
    }
    if (url.pathname === '/state') {
      return Response.json({
        packets: this.ctx.storage.kv.get('packets') ?? [],
        urls: this.ctx.storage.kv.get('urls') ?? [],
        callbacks: this.ctx.storage.kv.get('callbacks') ?? [],
        replies: this.ctx.storage.kv.get('replies') ?? [],
        edits: this.ctx.storage.kv.get('edits') ?? [],
        decisions: this.ctx.storage.kv.get('decisions') ?? 0,
        discoveries: this.ctx.storage.kv.get('discoveries') ?? 0,
        callbackTimes: this.ctx.storage.kv.get('callbackTimes') ?? [],
        closeCodes: this.ctx.storage.kv.get('closeCodes') ?? [],
        connections: [...this.sockets].filter((socket) => socket.readyState === WebSocket.OPEN)
          .length,
      });
    }
    if (url.pathname === '/release') {
      this.discoveryWaiters.splice(0).forEach((resolve) => resolve());
      return new Response(null, { status: 204 });
    }
    if (url.pathname === '/emit') {
      const packet = await request.text();
      for (const socket of this.sockets)
        if (socket.readyState === WebSocket.OPEN) socket.send(packet);
      return new Response(null, { status: 204 });
    }
    if (url.pathname === '/disconnect') {
      const { code } = await request.json<{ code: number }>();
      for (const socket of this.sockets) socket.close(code, 'Test disconnection');
      return new Response(null, { status: 204 });
    }
    if (url.pathname === '/api/v10/gateway/bot') {
      this.ctx.storage.kv.put(
        'discoveries',
        (this.ctx.storage.kv.get<number>('discoveries') ?? 0) + 1,
      );
      if (options.holdDiscovery)
        await new Promise<void>((resolve) => this.discoveryWaiters.push(resolve));
      if (options.discoveryStatus !== 200)
        return new Response('Rejected', { status: options.discoveryStatus });
      return Response.json({
        url: 'wss://gateway.discord.gg',
        shards: 1,
        session_start_limit: {
          total: 1000,
          remaining: options.remaining,
          reset_after: 600_000,
          max_concurrency: 1,
        },
      });
    }
    if (url.pathname.endsWith('/callback')) {
      const callbacks = this.ctx.storage.kv.get<unknown[]>('callbacks') ?? [];
      callbacks.push(await request.json());
      this.ctx.storage.kv.put('callbacks', callbacks);
      const times = this.ctx.storage.kv.get<number[]>('callbackTimes') ?? [];
      times.push(Date.now());
      this.ctx.storage.kv.put('callbackTimes', times);
      return new Response(null, { status: 204 });
    }
    if (url.hostname === 'cdn.discordapp.com') {
      return new Response(new Uint8Array([137, 80, 78, 71]), {
        headers: { 'Content-Type': 'image/png' },
      });
    }
    if (url.pathname.endsWith('/messages/@original')) {
      const form = await request.formData();
      const files = await Promise.all(
        [...form.entries()]
          .filter(([name]) => name.startsWith('files['))
          .map(async ([name, file]) => ({
            name,
            filename: (file as File).name,
            type: (file as File).type,
            bytes: [...new Uint8Array(await (file as File).arrayBuffer())],
          })),
      );
      const edits = this.ctx.storage.kv.get<unknown[]>('edits') ?? [];
      edits.push({
        method: request.method,
        path: url.pathname,
        body: JSON.parse(form.get('payload_json') as string),
        files,
        at: Date.now(),
      });
      this.ctx.storage.kv.put('edits', edits);
      return Response.json({ id: 'original' });
    }
    if (url.pathname.endsWith('/typing')) return new Response(null, { status: 204 });
    if (url.pathname.endsWith('/messages')) {
      if (request.method === 'GET') return Response.json([]);
      const replies = this.ctx.storage.kv.get<unknown[]>('replies') ?? [];
      replies.push(await request.json());
      this.ctx.storage.kv.put('replies', replies);
      return Response.json({ id: 'reply' });
    }
    if (url.pathname === '/v1/responses') {
      if (options.responseDelay)
        await new Promise((resolve) => setTimeout(resolve, options.responseDelay));
      return Response.json({
        object: 'response',
        output: [{ type: 'message', content: [{ type: 'output_text', text: 'Hey!' }] }],
      });
    }
    if (url.pathname === '/v1/decisions') {
      this.ctx.storage.kv.put('decisions', (this.ctx.storage.kv.get<number>('decisions') ?? 0) + 1);
      if (options.decisionDelay)
        await new Promise((resolve) => setTimeout(resolve, options.decisionDelay));
      return Response.json({ answers: [{ type: 'choice', choice: 'yes' }] });
    }
    if (request.headers.get('Upgrade') !== 'websocket') {
      return new Response('Unexpected outbound request', { status: 502 });
    }
    if (options.openDelay) await new Promise((resolve) => setTimeout(resolve, options.openDelay));
    const urls = this.ctx.storage.kv.get<string[]>('urls') ?? [];
    urls.push(request.url);
    this.ctx.storage.kv.put('urls', urls);
    if (url.hostname === 'resume.discord.gg' && options.resumeStatus !== 101) {
      return new Response('Resume endpoint unavailable', { status: options.resumeStatus });
    }
    const [client, server] = Object.values(new WebSocketPair()) as [WebSocket, WebSocket];
    server.accept();
    this.sockets.add(server);
    let sessionId: string | undefined;
    server.addEventListener('close', (event) => {
      this.sockets.delete(server);
      const codes = this.ctx.storage.kv.get<number[]>('closeCodes') ?? [];
      codes.push(event.code);
      this.ctx.storage.kv.put('closeCodes', codes);
      if (sessionId && (event.code === 1000 || event.code === 1001)) {
        this.ctx.storage.kv.put(`session:${sessionId}`, false);
      }
    });
    server.addEventListener('message', (event) => {
      const packet = JSON.parse(event.data as string);
      const packets = this.ctx.storage.kv.get<unknown[]>('packets') ?? [];
      packets.push({ ...packet, receivedAt: Date.now() });
      this.ctx.storage.kv.put('packets', packets);
      if (packet.op === 2 && !options.holdReady) {
        const identifies = (this.ctx.storage.kv.get<number>('identifies') ?? 0) + 1;
        this.ctx.storage.kv.put('identifies', identifies);
        sessionId = `fake-session-${identifies}`;
        this.ctx.storage.kv.put(`session:${sessionId}`, true);
        server.send(
          JSON.stringify({
            op: 0,
            t: 'READY',
            s: 1,
            d: { session_id: sessionId, resume_gateway_url: 'wss://resume.discord.gg' },
          }),
        );
        server.send(JSON.stringify({ op: 11 }));
      }
      if (packet.op === 6) {
        sessionId = packet.d.session_id;
        if (this.ctx.storage.kv.get(`session:${sessionId}`)) {
          server.send(JSON.stringify({ op: 0, t: 'RESUMED', s: packet.d.seq + 1, d: {} }));
        } else {
          server.send(JSON.stringify({ op: 9, d: false }));
        }
      }
      if (packet.op === 1) server.send(JSON.stringify({ op: 11 }));
    });
    server.send(JSON.stringify({ op: 10, d: { heartbeat_interval: 45_000 } }));
    return new Response(null, { status: 101, webSocket: client });
  }
}

export default {
  fetch(request: Request, env: { DISCORD: DurableObjectNamespace<FakeDiscord> }) {
    return env.DISCORD.getByName('discord').fetch(request);
  },
};
