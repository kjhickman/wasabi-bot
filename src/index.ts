import app from './app.ts';
import type { Env } from './env.ts';
import { gateway } from './gateway/stub.ts';

export { DiscordGateway } from './gateway/durable-object.ts';
export { RadioCallback, RadioPlayer } from './radio/player.ts';

export default {
  fetch: app.fetch,
  async scheduled(_controller: ScheduledController, env: Env) {
    await gateway(env).ensureConnected();
  },
} satisfies ExportedHandler<Env>;
