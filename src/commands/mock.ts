import { ApplicationCommandType } from 'discord-api-types/v10';
import { MAX_MESSAGE_LENGTH } from '../discord.ts';
import { CommandError, reply, type MessageCommand } from './command.ts';

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
    return reply({ content: mocked.slice(0, MAX_MESSAGE_LENGTH), allowed_mentions: { parse: [] } });
  },
} satisfies MessageCommand;
