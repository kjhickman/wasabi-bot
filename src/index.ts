import app, { gateway } from './app.ts';
import type { Env } from './env.ts';

export { DiscordGateway } from './gateway/durable-object.ts';

export default {
  fetch: app.fetch,
  async scheduled(_controller: ScheduledController, env: Env) {
    await gateway(env).ensureConnected();
  },
} satisfies ExportedHandler<Env>;
