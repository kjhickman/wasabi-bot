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
    if (path === '/_test/crash') {
      try {
        await gateway.crash();
      } catch {
        return new Response(null, { status: 204 });
      }
    }
    return worker.fetch(request, env, ctx);
  },
};
