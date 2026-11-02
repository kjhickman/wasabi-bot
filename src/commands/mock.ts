import {
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIInteractionResponseChannelMessageWithSource,
  type APIMessageApplicationCommandInteractionData,
  type RESTPostAPIContextMenuApplicationCommandsJSONBody,
} from 'discord-api-types/v10';

// Nitro members can send 4,000 characters, but bots are limited to 2,000.
const MAX_MESSAGE_LENGTH = 2_000;

export const mock = {
  definition: {
    name: 'Mock',
    type: ApplicationCommandType.Message,
  } satisfies RESTPostAPIContextMenuApplicationCommandsJSONBody,
  // Discord doesn't allow descriptions on message commands, so this is only shown by /help.
  description: 'Repeat a message in aLtErNaTiNg CaSe.',

  execute(
    data: APIMessageApplicationCommandInteractionData,
  ): APIInteractionResponseChannelMessageWithSource {
    const text = data.resolved.messages[data.target_id]?.content.trim();
    if (!text) {
      return {
        type: InteractionResponseType.ChannelMessageWithSource,
        data: { content: 'That message has no text to mock.', flags: MessageFlags.Ephemeral },
      };
    }

    let letters = 0;
    const mocked = text.replace(/\p{L}/gu, (letter) =>
      letters++ % 2 ? letter.toLowerCase() : letter.toUpperCase(),
    );
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: mocked.slice(0, MAX_MESSAGE_LENGTH), allowed_mentions: { parse: [] } },
    };
  },
};
