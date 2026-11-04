import { GatewayDispatchEvents, type GatewayDispatchPayload } from 'discord-api-types/v10';
import { addressesBot, replyToMessage, type ChatOptions } from '../chat/index.ts';
import { claim } from '../storage.ts';
import { handleInteraction } from './interactions.ts';
import { clearVoiceStates, replaceVoiceStates, saveVoiceState } from './voice-states.ts';

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
  if (packet.t === GatewayDispatchEvents.Ready) return clearVoiceStates(sql);
  if (packet.t === GatewayDispatchEvents.GuildCreate) {
    // An unavailable guild comes without voice states and is sent again once it's back.
    const states = 'voice_states' in packet.d ? packet.d.voice_states : [];
    return replaceVoiceStates(sql, packet.d.id, states);
  }
  if (packet.t === GatewayDispatchEvents.GuildDelete) {
    return replaceVoiceStates(sql, packet.d.id, []);
  }
  if (packet.t === GatewayDispatchEvents.VoiceStateUpdate && packet.d.guild_id) {
    saveVoiceState(sql, packet.d.guild_id, packet.d);
    // The radio's voice connection needs the bot's own voice state and server.
    if (packet.d.user_id === bot.id) {
      return env.RADIO_PLAYER.getByName(packet.d.guild_id).voiceEvent(packet);
    }
  }
  if (packet.t === GatewayDispatchEvents.VoiceServerUpdate) {
    return env.RADIO_PLAYER.getByName(packet.d.guild_id).voiceEvent(packet);
  }
}
