import { ApplicationCommandType } from 'discord-api-types/v10';
import { reply, type SlashCommand } from './command.ts';

export const flip = {
  definition: {
    name: 'flip',
    description: 'Flip a coin.',
    type: ApplicationCommandType.ChatInput,
  },

  execute() {
    const side = Math.random() < 0.5 ? 'Heads' : 'Tails';
    return reply({ content: `🪙 **${side}**!` });
  },
} satisfies SlashCommand;
