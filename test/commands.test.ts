import { ApplicationCommandType, InteractionResponseType } from 'discord-api-types/v10';
import { afterEach, describe, expect, it, vi } from 'vite-plus/test';
import { CommandError } from '../src/commands/command.ts';
import { conch } from '../src/commands/conch.ts';
import { commands, getCommand, messageCommands, runCommand } from '../src/commands/index.ts';
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

  it('rejects an unknown command', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'unknown' } as const;

    await expect(runCommand(data, env, signal)).rejects.toEqual(
      new CommandError('Unknown command.'),
    );
  });
});

describe('/help', () => {
  it('lists every slash and message command with its description', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'help' } as const;
    const { content } = (await runCommand(data, env, signal)).data;

    for (const { definition } of commands) {
      expect(content).toContain(`\`/${definition.name}\`: ${definition.description}`);
    }
    for (const { definition, description } of messageCommands) {
      expect(content).toContain(`**${definition.name}**`);
      expect(content).toContain(description);
    }
  });
});

describe('deferred commands', () => {
  it.each([
    ['caption', true],
    ['imagine', true],
    ['edit', true],
    ['conch', false],
    ['unknown', false],
  ])('%s → %s', (name, deferred) => {
    expect(getCommand(name)?.deferred ?? false).toBe(deferred);
  });
});
