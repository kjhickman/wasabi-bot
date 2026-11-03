import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIApplicationCommandStringOption,
} from 'discord-api-types/v10';
import type { SlashCommand } from './command.ts';

const MIN_OPTIONS = 2;
const MAX_OPTIONS = 7;

export const choose = {
  definition: {
    name: 'choose',
    description: 'Randomly pick one of 2 to 7 options.',
    type: ApplicationCommandType.ChatInput,
    options: Array.from({ length: MAX_OPTIONS }, (_, i): APIApplicationCommandStringOption => ({
      type: ApplicationCommandOptionType.String,
      name: `option${i + 1}`,
      description: `Option ${i + 1}`,
      required: i < MIN_OPTIONS,
      min_length: 1,
      max_length: 100,
    })),
  },

  execute(data) {
    const choices = (data.options ?? []).flatMap((option) =>
      option.type === ApplicationCommandOptionType.String && option.value.trim()
        ? [option.value.trim()]
        : [],
    );
    if (choices.length < MIN_OPTIONS) {
      return {
        type: InteractionResponseType.ChannelMessageWithSource,
        data: {
          content: `Give me at least ${MIN_OPTIONS} options to choose from.`,
          flags: MessageFlags.Ephemeral,
        },
      };
    }

    const choice = choices[Math.floor(Math.random() * choices.length)]!;
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: {
        content: `Choosing between: ${choices.join(', ')}\nI choose **${choice}**!`,
        allowed_mentions: { parse: [] },
      },
    };
  },
} satisfies SlashCommand;
