import {
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIMessageApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { caption } from './caption.ts';
import { choose } from './choose.ts';
import type { CommandResponse, MessageCommand, SlashCommand } from './command.ts';
import { conch } from './conch.ts';
import { flip } from './flip.ts';
import { edit, imagine } from './image.ts';
import { mock } from './mock.ts';

// Lives here rather than in its own file, since it needs the command list.
const help = {
  definition: {
    name: 'help',
    description: "List the bot's commands.",
    type: ApplicationCommandType.ChatInput,
  },

  execute() {
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
} satisfies SlashCommand;

export const commands: SlashCommand[] = [conch, choose, caption, imagine, edit, flip, help];
export const messageCommands: MessageCommand[] = [mock];

const commandsByName = new Map(commands.map((command) => [command.definition.name, command]));

export function getCommand(name: string) {
  return commandsByName.get(name);
}

export function withFiles(message: object, files: File[]) {
  const body = new FormData();
  body.set(
    'payload_json',
    JSON.stringify({
      ...message,
      attachments: files.map((file, id) => ({ id, filename: file.name })),
    }),
  );
  files.forEach((file, i) => body.set(`files[${i}]`, file));
  return body;
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
    const command = getCommand(data.name);
    if (command) return command.execute(data, env, signal);
  }
  return {
    type: InteractionResponseType.ChannelMessageWithSource,
    data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
  };
}
