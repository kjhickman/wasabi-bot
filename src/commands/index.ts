import {
  InteractionResponseType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteractionResponseChannelMessageWithSource,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { choose } from './choose.ts';
import { conch } from './conch.ts';
import { ping } from './ping.ts';

export const commands = [ping, conch, choose];

export async function runCommand(
  data: APIChatInputApplicationCommandInteractionData,
  env: Env,
  signal: AbortSignal,
): Promise<APIInteractionResponseChannelMessageWithSource> {
  const command = commands.find((command) => command.definition.name === data.name);
  if (!command) {
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
    };
  }
  return command.execute(data, env, signal);
}
