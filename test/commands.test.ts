import { ApplicationCommandType } from 'discord-api-types/v10';
import { describe, expect, it } from 'vite-plus/test';
import { CommandError } from '../src/commands/command.ts';
import { commands, getCommand, messageCommands, runCommand } from '../src/commands/index.ts';
import type { Env } from '../src/env.ts';

const env = {} as Env;
const signal = new AbortController().signal;
const invocation = { guildId: '300', userId: '200' };

describe('runCommand', () => {
  it('rejects an unknown command', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'unknown' } as const;

    await expect(runCommand(data, env, signal, invocation)).rejects.toEqual(
      new CommandError('Unknown command.'),
    );
  });
});

describe('/help', () => {
  it('lists every slash and message command with its description', async () => {
    const data = { id: '1', type: ApplicationCommandType.ChatInput, name: 'help' } as const;
    const { content } = (await runCommand(data, env, signal, invocation)).data;

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
  it.each(['imagine', 'edit'])(
    'defers /%s, since images take longer than Discord waits',
    (name) => {
      expect(getCommand(name)?.deferred).toBe(true);
    },
  );
});
