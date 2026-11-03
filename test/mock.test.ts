import {
  ApplicationCommandType,
  InteractionResponseType,
  type APIMessage,
} from 'discord-api-types/v10';
import { describe, expect, it } from 'vite-plus/test';
import { CommandError } from '../src/commands/command.ts';
import { mock } from '../src/commands/mock.ts';

function run(content: string) {
  return mock.execute({
    id: '1',
    type: ApplicationCommandType.Message,
    name: 'Mock',
    target_id: '800',
    resolved: { messages: { '800': { content } as APIMessage } },
  });
}

describe('Mock', () => {
  it('alternates the case of letters only, without pinging anyone', () => {
    expect(run('hello, world! 123 émoji')).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'HeLlO, wOrLd! 123 ÉmOjI', allowed_mentions: { parse: [] } },
    });
  });

  it('keeps the reply within a bot’s message limit', () => {
    expect(run('a'.repeat(4_000)).data.content).toHaveLength(2_000);
  });

  it.each(['', '   '])('rejects a message without text: %j', (content) => {
    expect(() => run(content)).toThrow(new CommandError('That message has no text to mock.'));
  });
});
