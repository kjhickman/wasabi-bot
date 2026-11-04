import { DISCORD_EPOCH_MS } from '../../src/discord.ts';

export function snowflake(createdAt = Date.now(), increment = 0) {
  return ((BigInt(createdAt - DISCORD_EPOCH_MS) << 22n) + BigInt(increment)).toString();
}

export const chooseArgs = {
  option1: 'WoW',
  option2: 'Overwatch',
  option3: null,
  option4: null,
  option5: null,
  option6: null,
  option7: null,
};
