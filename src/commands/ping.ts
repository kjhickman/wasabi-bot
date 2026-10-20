import {
  ApplicationCommandType,
  InteractionResponseType,
  type APIInteractionResponseChannelMessageWithSource,
} from 'discord-api-types/payloads/v10';
import type { RESTPostAPIChatInputApplicationCommandsJSONBody } from 'discord-api-types/v10';

export const ping = {
  definition: {
    name: 'ping',
    description: 'Check whether the bot is responding.',
    type: ApplicationCommandType.ChatInput,
  } satisfies RESTPostAPIChatInputApplicationCommandsJSONBody,

  execute(): APIInteractionResponseChannelMessageWithSource {
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Pong!' },
    };
  },
};
