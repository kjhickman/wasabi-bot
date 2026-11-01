import app from './app.ts';
import type { Env } from './env.ts';

export { DiscordGateway } from './gateway/durable-object.ts';

export default {
  fetch: app.fetch,
  async scheduled(_controller: ScheduledController, env: Env) {
    await env.DISCORD_GATEWAY.getByName('gateway').ensureConnected();
  },
} satisfies ExportedHandler<Env>;
