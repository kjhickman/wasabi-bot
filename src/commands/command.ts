import type {
  APIChatInputApplicationCommandInteractionData,
  APIInteractionResponseChannelMessageWithSource,
  APIMessageApplicationCommandInteractionData,
  RESTPostAPIChatInputApplicationCommandsJSONBody,
  RESTPostAPIContextMenuApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import type { Kind } from '../usage.ts';

// A failure to show the person who ran the command, such as bad input. It doesn't count
// toward their daily limit.
export class CommandError extends Error {}

// Slash commands only upload files when deferred, since their replies are sent as edits.
export type CommandResponse = APIInteractionResponseChannelMessageWithSource & { files?: File[] };

export interface SlashCommand {
  definition: RESTPostAPIChatInputApplicationCommandsJSONBody;
  // Deferred commands may take longer than Discord's 3-second window, so they acknowledge
  // the interaction first and edit in their reply later.
  deferred?: boolean;
  // Commands that call OpenAI count toward a daily limit (see usage.ts).
  limit?: Kind;
  // Chat sends only the command's text, not its files.
  textOnlyInChat?: boolean;
  execute(
    data: APIChatInputApplicationCommandInteractionData,
    env: Env,
    signal: AbortSignal,
  ): CommandResponse | Promise<CommandResponse>;
}

// Message commands are run from a message's Apps menu.
export interface MessageCommand {
  definition: RESTPostAPIContextMenuApplicationCommandsJSONBody;
  // Discord doesn't allow descriptions on message commands, so this is only shown by /help.
  description: string;
  execute(data: APIMessageApplicationCommandInteractionData): CommandResponse;
}
