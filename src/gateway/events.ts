import { GatewayDispatchEvents, type GatewayDispatchPayload } from 'discord-api-types/v10';
import { addressesBot, replyToMention } from '../chat/index.ts';
import type { Env } from '../env.ts';
import { claim } from '../storage.ts';
import { handleInteraction } from './interactions.ts';

// Runs the bot's features for the Gateway events it acts on.
export async function handleEvent(
  packet: GatewayDispatchPayload,
  token: string,
  env: Env,
  sql: SqlStorage,
) {
  if (packet.t === GatewayDispatchEvents.InteractionCreate) {
    return handleInteraction(packet.d, env, sql);
  }
  if (
    packet.t === GatewayDispatchEvents.MessageCreate &&
    addressesBot(packet.d, token) &&
    claim(sql, packet.d.id)
  ) {
    return replyToMention(packet.d, { token, env, sql });
  }
}
