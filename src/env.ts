import type { DiscordGateway } from './gateway/durable-object.ts';

export interface Env {
  DISCORD_GATEWAY: DurableObjectNamespace<DiscordGateway>;
  GATEWAY_MODE: 'dev' | 'prod';
  DISCORD_BOT_TOKEN?: string;
  GATEWAY_CONTROL_TOKEN?: string;
  OPENAI_API_KEY?: string;
}
