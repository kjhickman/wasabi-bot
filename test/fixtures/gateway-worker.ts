import worker, { DiscordGateway } from '../../src/index.ts';
import type { Env } from '../../src/env.ts';
import type { GatewayConnection } from '../../src/gateway/connection.ts';

export class TestGateway extends DiscordGateway {
  crash(): void {
    this.ctx.abort('Test Gateway restart');
  }

  // Kill the socket without the Durable Object hearing about it.
  zombie(): void {
    (this as unknown as { connection: GatewayConnection }).connection.close(3010);
  }

  async expireLease(): Promise<void> {
    const lifecycle = this.ctx.storage.kv.get<Record<string, unknown>>('lifecycle')!;
    this.ctx.storage.kv.put('lifecycle', { ...lifecycle, expiresAt: Date.now() - 1 });
    await this.ctx.storage.sync();
    this.ctx.abort('Test lease expiry and restart');
  }

  alarmTime() {
    return this.ctx.storage.getAlarm();
  }
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext) {
    const path = new URL(request.url).pathname;
    const gateway = (env.DISCORD_GATEWAY as DurableObjectNamespace<TestGateway>).getByName(
      'gateway',
    );
    if (path === '/_test/reconcile') {
      await worker.scheduled({} as ScheduledController, env);
      return new Response(null, { status: 204 });
    }
    if (path === '/_test/alarm') return Response.json(await gateway.alarmTime());
    if (path === '/_test/zombie') {
      await gateway.zombie();
      return new Response(null, { status: 204 });
    }
    if (path === '/_test/crash' || path === '/_test/expire') {
      try {
        if (path === '/_test/crash') await gateway.crash();
        else await gateway.expireLease();
      } catch {
        return new Response(null, { status: 204 });
      }
    }
    return worker.fetch(request, env, ctx);
  },
};
