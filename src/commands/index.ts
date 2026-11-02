import {
  InteractionResponseType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteractionResponseChannelMessageWithSource,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { caption } from './caption.ts';
import { choose } from './choose.ts';
import { conch } from './conch.ts';

export const commands = [conch, choose, caption];

// Files are only uploaded for deferred commands, whose replies are sent as edits.
export type CommandResponse = APIInteractionResponseChannelMessageWithSource & { files?: File[] };

// Deferred commands may take longer than Discord's 3-second window, so they acknowledge
// the interaction first and edit in their reply later.
export function isDeferred(name: string) {
  return commands.some((command) => command.definition.name === name && 'deferred' in command);
}

export async function runCommand(
  data: APIChatInputApplicationCommandInteractionData,
  env: Env,
  signal: AbortSignal,
): Promise<CommandResponse> {
  const command = commands.find((command) => command.definition.name === data.name);
  if (!command) {
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
    };
  }
  return command.execute(data, env, signal);
}
