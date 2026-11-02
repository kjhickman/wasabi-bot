import {
  ApplicationCommandType,
  InteractionResponseType,
  type APIInteractionResponseChannelMessageWithSource,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';

export const flip = {
  definition: {
    name: 'flip',
    description: 'Flip a coin.',
    type: ApplicationCommandType.ChatInput,
  } satisfies RESTPostAPIChatInputApplicationCommandsJSONBody,

  execute(): APIInteractionResponseChannelMessageWithSource {
    const side = Math.random() < 0.5 ? 'Heads' : 'Tails';
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: `🪙 **${side}**!` },
    };
  },
};
