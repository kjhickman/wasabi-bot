import { GatewayDispatchEvents, type GatewayDispatchPayload } from 'discord-api-types/v10';
import { addressesBot, replyToMessage, type ChatOptions } from '../chat/index.ts';
import { claim } from '../storage.ts';
import { handleInteraction } from './interactions.ts';

// Runs the bot's features for the Gateway events it acts on.
export async function handleEvent(packet: GatewayDispatchPayload, context: ChatOptions) {
  const { bot, env, sql } = context;
  if (packet.t === GatewayDispatchEvents.InteractionCreate) {
    return handleInteraction(packet.d, env, sql);
  }
  if (
    packet.t === GatewayDispatchEvents.MessageCreate &&
    addressesBot(packet.d, bot) &&
    claim(sql, packet.d.id)
  ) {
    return replyToMessage(packet.d, context);
  }
}
