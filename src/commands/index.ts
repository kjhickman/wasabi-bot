import {
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteractionResponseChannelMessageWithSource,
  type APIMessageApplicationCommandInteractionData,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { caption } from './caption.ts';
import { choose } from './choose.ts';
import { conch } from './conch.ts';
import { flip } from './flip.ts';
import { mock } from './mock.ts';

// Lives here rather than in its own file, since it needs the command list.
const help = {
  definition: {
    name: 'help',
    description: "List the bot's commands.",
    type: ApplicationCommandType.ChatInput,
  } satisfies RESTPostAPIChatInputApplicationCommandsJSONBody,

  execute(): APIInteractionResponseChannelMessageWithSource {
    const lines = [
      ...commands.map(({ definition }) => `- \`/${definition.name}\`: ${definition.description}`),
      ...messageCommands.map(
        ({ definition, description }) =>
          `- **${definition.name}** (right-click a message, then Apps): ${description}`,
      ),
    ];
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: lines.join('\n') },
    };
  },
};

export const commands = [conch, choose, caption, flip, help];
// Message commands are run from a message's Apps menu.
export const messageCommands = [mock];

// Files are only uploaded for deferred commands, whose replies are sent as edits.
export type CommandResponse = APIInteractionResponseChannelMessageWithSource & { files?: File[] };

// Deferred commands may take longer than Discord's 3-second window, so they acknowledge
// the interaction first and edit in their reply later.
export function isDeferred(name: string) {
  return commands.some((command) => command.definition.name === name && 'deferred' in command);
}

export async function runCommand(
  data: APIChatInputApplicationCommandInteractionData | APIMessageApplicationCommandInteractionData,
  env: Env,
  signal: AbortSignal,
): Promise<CommandResponse> {
  if (data.type === ApplicationCommandType.Message) {
    const command = messageCommands.find((command) => command.definition.name === data.name);
    if (command) return command.execute(data);
  } else {
    const command = commands.find((command) => command.definition.name === data.name);
    if (command) return command.execute(data, env, signal);
  }
  return {
    type: InteractionResponseType.ChannelMessageWithSource,
    data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
  };
}
