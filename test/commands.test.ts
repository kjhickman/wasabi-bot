import {
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
} from 'discord-api-types/v10';
import { afterEach, describe, expect, it, vi } from 'vite-plus/test';
import { conch } from '../src/commands/conch.ts';
import { isDeferred, runCommand } from '../src/commands/index.ts';
import type { Env } from '../src/env.ts';

const env = {} as Env;
const signal = new AbortController().signal;

afterEach(() => {
  vi.restoreAllMocks();
});

describe('runCommand', () => {
  it('passes the interaction data, env, and deadline to the matching command', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'conch' } as const;
    const response = {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Yes' },
    } as const;
    const execute = vi.spyOn(conch, 'execute').mockResolvedValue(response);

    expect(await runCommand(data, env, signal)).toBe(response);
    expect(execute).toHaveBeenCalledExactlyOnceWith(data, env, signal);
  });

  it('rejects an unknown command privately', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'unknown' } as const;

    expect(await runCommand(data, env, signal)).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
    });
  });
});

describe('isDeferred', () => {
  it.each([
    ['caption', true],
    ['conch', false],
    ['unknown', false],
  ])('%s → %s', (name, deferred) => {
    expect(isDeferred(name)).toBe(deferred);
  });
});
