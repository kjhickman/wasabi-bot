import type { DiscordGateway } from './gateway/durable-object.ts';
import type { RadioPlayer } from './radio/player.ts';

export interface Env {
  DISCORD_GATEWAY: DurableObjectNamespace<DiscordGateway>;
  RADIO_PLAYER: DurableObjectNamespace<RadioPlayer>;
  GATEWAY_MODE: 'dev' | 'prod';
  DISCORD_BOT_TOKEN?: string;
  GATEWAY_CONTROL_TOKEN?: string;
  OPENAI_API_KEY?: string;
}

declare global {
  namespace Cloudflare {
    interface GlobalProps {
      mainModule: typeof import('./index.ts');
    }
  }
}
