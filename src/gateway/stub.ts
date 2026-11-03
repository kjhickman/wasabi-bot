import type { Env } from '../env.ts';

export const GATEWAY_NAME = 'gateway-enam';

export function gateway(env: Env) {
  return env.DISCORD_GATEWAY.getByName(GATEWAY_NAME, { locationHint: 'enam' });
}
