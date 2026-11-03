import { ApplicationCommandType, InteractionResponseType } from 'discord-api-types/v10';
import { CommandError, type MessageCommand } from './command.ts';

// Nitro members can send 4,000 characters, but bots are limited to 2,000.
const MAX_MESSAGE_LENGTH = 2_000;

export const mock = {
  definition: {
    name: 'Mock',
    type: ApplicationCommandType.Message,
  },
  description: 'Repeat a message in aLtErNaTiNg CaSe.',

  execute(data) {
    const text = data.resolved.messages[data.target_id]?.content.trim();
    if (!text) throw new CommandError('That message has no text to mock.');

    let letters = 0;
    const mocked = text.replace(/\p{L}/gu, (letter) =>
      letters++ % 2 ? letter.toLowerCase() : letter.toUpperCase(),
    );
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: mocked.slice(0, MAX_MESSAGE_LENGTH), allowed_mentions: { parse: [] } },
    };
  },
} satisfies MessageCommand;
