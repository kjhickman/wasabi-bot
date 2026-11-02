import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIApplicationCommandInteractionDataOption,
} from 'discord-api-types/v10';
import { afterEach, describe, expect, it, vi } from 'vite-plus/test';
import { choose } from '../src/commands/choose.ts';

const option = (n: number, value: string): APIApplicationCommandInteractionDataOption => ({
  type: ApplicationCommandOptionType.String,
  name: `option${n}`,
  value,
});

function pick(options: APIApplicationCommandInteractionDataOption[] | undefined) {
  const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'choose' } as const;
  return choose.execute(options ? { ...data, options } : data);
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('/choose', () => {
  it.each([
    [0, 'Halo'],
    [0.34, 'Minecraft'],
    [0.999999, 'Rocket League'],
  ])('maps random %s to %s with equal weight', (random, choice) => {
    vi.spyOn(Math, 'random').mockReturnValue(random);

    expect(pick([option(1, 'Halo'), option(2, ' Minecraft '), option(3, 'Rocket League')])).toEqual(
      {
        type: InteractionResponseType.ChannelMessageWithSource,
        data: {
          content: `Choosing between: Halo, Minecraft, Rocket League\nI choose **${choice}**!`,
          allowed_mentions: { parse: [] },
        },
      },
    );
  });

  it('picks from all 7 options', () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.999999);
    const options = [1, 2, 3, 4, 5, 6, 7].map((n) => option(n, `Game ${n}`));

    expect(pick(options).data.content).toContain('I choose **Game 7**!');
  });

  it.each<{ options: APIApplicationCommandInteractionDataOption[] | undefined }>([
    { options: undefined },
    { options: [option(1, 'Halo')] },
    { options: [option(1, 'Halo'), option(2, '   ')] },
  ])('rejects fewer than 2 options privately: %j', ({ options }) => {
    expect(pick(options)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: {
        content: 'Give me at least 2 options to choose from.',
        flags: MessageFlags.Ephemeral,
      },
    });
  });
});
