import worker, { DiscordGateway } from '../../src/index.ts';
import type { Env } from '../../src/env.ts';
import type { Session } from '../../src/gateway/connection.ts';
import { gateway as gatewayStub } from '../../src/gateway/stub.ts';

export class TestGateway extends DiscordGateway {
  // Shorter than production so tests don't sit through real backoffs; gateway.test.ts timings assume these.
  protected override readonly identifyCooldownMs = 1_000;
  protected override readonly retryBaseMs = 100;

  crash(): void {
    this.ctx.abort('Test Gateway restart');
  }

  // Kill the socket without the Durable Object hearing about it.
  zombie(): void {
    this.connection!.close(3010);
  }

  alarmTime() {
    return this.ctx.storage.getAlarm();
  }

  // Run the alarm with no alarm set and storage that makes pruning throw.
  async failAlarm() {
    await this.ctx.storage.deleteAlarm();
    this.ctx.storage.sql.exec('DROP TABLE seen');
    await this.alarm();
  }

  forgetSessionUser(): void {
    const lifecycle = this.ctx.storage.kv.get<{ session: Partial<Session> }>('lifecycle')!;
    delete lifecycle.session.user;
    this.ctx.storage.kv.put('lifecycle', lifecycle);
  }
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext) {
    const path = new URL(request.url).pathname;
    const gateway = gatewayStub(env) as unknown as DurableObjectStub<TestGateway>;
    if (path === '/_test/reconcile') {
      await worker.scheduled({} as ScheduledController, env);
      return new Response(null, { status: 204 });
    }
    if (path === '/_test/alarm') return Response.json(await gateway.alarmTime());
    if (path === '/_test/fail-alarm') {
      await gateway.failAlarm();
      return new Response(null, { status: 204 });
    }
    if (path === '/_test/zombie') {
      await gateway.zombie();
      return new Response(null, { status: 204 });
    }
    if (path === '/_test/legacy-session') {
      await gateway.forgetSessionUser();
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
