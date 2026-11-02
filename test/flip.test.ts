import { InteractionResponseType } from 'discord-api-types/v10';
import { afterEach, describe, expect, it, vi } from 'vite-plus/test';
import { flip } from '../src/commands/flip.ts';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('/flip', () => {
  it.each([
    [0.499999, 'Heads'],
    [0.5, 'Tails'],
  ])('maps random %s to %s', (random, side) => {
    vi.spyOn(Math, 'random').mockReturnValue(random);

    expect(flip.execute()).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: `🪙 **${side}**!` },
    });
  });
});
