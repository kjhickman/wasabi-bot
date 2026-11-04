import {
  InteractionResponseType,
  type APIApplicationCommandAutocompleteInteraction,
  type APIApplicationCommandOptionChoice,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteractionResponseCallbackData,
  type APIInteractionResponseChannelMessageWithSource,
  type APIMessageApplicationCommandInteractionData,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
  type RESTPostAPIContextMenuApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { Env } from '../env.ts';
import type { UsageKind } from '../usage.ts';

// A failure to show the person who ran the command, such as bad input. It doesn't count
// toward their daily limit.
export class CommandError extends Error {}

// Slash commands only upload files when deferred, since their replies are sent as edits.
export type CommandResponse = APIInteractionResponseChannelMessageWithSource & { files?: File[] };

export function reply(data: APIInteractionResponseCallbackData, files?: File[]): CommandResponse {
  return { type: InteractionResponseType.ChannelMessageWithSource, data, ...(files && { files }) };
}

// Who ran a command, and in which server. Chat runs commands for a user outside any
// interaction, so it has no server to give and no 3-second window to defer.
export interface Invocation {
  guildId?: string | undefined;
  userId?: string | undefined;
  // Acknowledges the interaction so the rest of the command can take longer than Discord's
  // 3-second window. Errors before this are private; after it, they replace the public
  // "thinking" message.
  defer?(): Promise<void>;
}

export interface SlashCommand {
  definition: RESTPostAPIChatInputApplicationCommandsJSONBody;
  // Deferred commands may take longer than Discord's 3-second window, so they acknowledge
  // the interaction first and edit in their reply later.
  deferred?: boolean;
  // Commands that call OpenAI count toward a daily limit (see usage.ts).
  limit?: UsageKind;
  textOnlyInChat?: boolean;
  // Suggests values for an option marked `autocomplete` as the user types.
  autocomplete?(
    data: APIApplicationCommandAutocompleteInteraction['data'],
    env: Env,
    signal: AbortSignal,
  ): Promise<APIApplicationCommandOptionChoice[]>;
  execute(
    data: APIChatInputApplicationCommandInteractionData,
    env: Env,
    signal: AbortSignal,
    invocation: Invocation,
  ): CommandResponse | Promise<CommandResponse>;
}

export interface MessageCommand {
  definition: RESTPostAPIContextMenuApplicationCommandsJSONBody;
  // Discord doesn't allow descriptions on message commands, so this is only shown by /help.
  description: string;
  execute(data: APIMessageApplicationCommandInteractionData): CommandResponse;
}
