import { ApplicationCommandType, InteractionResponseType } from 'discord-api-types/v10';
import type { SlashCommand } from './command.ts';

export const flip = {
  definition: {
    name: 'flip',
    description: 'Flip a coin.',
    type: ApplicationCommandType.ChatInput,
  },

  execute() {
    const side = Math.random() < 0.5 ? 'Heads' : 'Tails';
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: `🪙 **${side}**!` },
    };
  },
} satisfies SlashCommand;
