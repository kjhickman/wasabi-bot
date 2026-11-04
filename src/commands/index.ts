import { tracing } from 'cloudflare:workers';
import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  type APIApplicationCommandInteractionDataOption,
  type APIChatInputApplicationCommandInteractionData,
  type APIMessageApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import { caption } from './caption.ts';
import { choose } from './choose.ts';
import {
  CommandError,
  reply,
  type CommandResponse,
  type Invocation,
  type MessageCommand,
  type SlashCommand,
} from './command.ts';
import { conch } from './conch.ts';
import { flip } from './flip.ts';
import { edit, imagine } from './image.ts';
import { mock } from './mock.ts';
import { radio } from './radio.ts';

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
    return reply({ content: lines.join('\n') });
  },
} satisfies SlashCommand;

export const commands: SlashCommand[] = [conch, choose, caption, imagine, edit, flip, radio, help];
export const messageCommands: MessageCommand[] = [mock];

const commandsByName = new Map(commands.map((command) => [command.definition.name, command]));

export function getCommand(name: string) {
  return commandsByName.get(name);
}

type CommandData =
  | APIChatInputApplicationCommandInteractionData
  | APIMessageApplicationCommandInteractionData;

// Each use of a command, from a slash command or from chat, gets a span named for it.
export function runCommand(
  data: CommandData,
  env: Env,
  signal: AbortSignal,
  invocation: Invocation,
): Promise<CommandResponse> {
  return tracing.enterSpan(commandName(data), async (span) => {
    try {
      const response = await dispatch(data, env, signal, invocation);
      span.setAttribute('outcome', 'ok');
      return response;
    } catch (error) {
      span.setAttribute('outcome', error instanceof CommandError ? 'command_error' : 'error');
      throw error;
    }
  });
}

// How a command is named in spans: the way someone uses it, like `/radio play` or `Mock`.
export function commandName(data: {
  type: ApplicationCommandType;
  name: string;
  options?: APIApplicationCommandInteractionDataOption[];
}) {
  if (data.type !== ApplicationCommandType.ChatInput) return data.name;
  const [subcommand] = data.options ?? [];
  return subcommand?.type === ApplicationCommandOptionType.Subcommand
    ? `/${data.name} ${subcommand.name}`
    : `/${data.name}`;
}

async function dispatch(
  data: CommandData,
  env: Env,
  signal: AbortSignal,
  invocation: Invocation,
): Promise<CommandResponse> {
  if (data.type === ApplicationCommandType.Message) {
    const command = messageCommands.find((command) => command.definition.name === data.name);
    if (command) return command.execute(data);
  } else {
    const command = getCommand(data.name);
    if (command) return command.execute(data, env, signal, invocation);
  }
  throw new CommandError('Unknown command.');
}
