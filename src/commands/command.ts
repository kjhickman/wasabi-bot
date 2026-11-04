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

export class CommandError extends Error {}

// Slash commands only upload files when deferred, since their replies are sent as edits.
export type CommandResponse = APIInteractionResponseChannelMessageWithSource & { files?: File[] };

export function reply(data: APIInteractionResponseCallbackData, files?: File[]): CommandResponse {
  return { type: InteractionResponseType.ChannelMessageWithSource, data, ...(files && { files }) };
}

export interface Invocation {
  guildId?: string | undefined;
  userId?: string | undefined;
  defer?(): Promise<void>;
}

export interface SlashCommand {
  definition: RESTPostAPIChatInputApplicationCommandsJSONBody;
  deferred?: boolean;
  limit?: UsageKind;
  textOnlyInChat?: boolean;
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
  description: string;
  execute(data: APIMessageApplicationCommandInteractionData): CommandResponse;
}
