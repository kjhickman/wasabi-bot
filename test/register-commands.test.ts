import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { conch } from '../src/commands/conch.ts';
import { ping } from '../src/commands/ping.ts';

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  vi.resetModules();
  fetchMock.mockReset();
  vi.stubEnv('DISCORD_APPLICATION_ID', '123456789012345678');
  vi.stubEnv('DISCORD_BOT_TOKEN', 'test-token');
  vi.stubEnv('DISCORD_GUILD_ID', undefined);
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'log').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('command registration', () => {
  it('replaces the global command list without a server ID', async () => {
    fetchMock.mockResolvedValue(Response.json([{ id: 'command-id' }]));

    await import('../scripts/register-commands.ts');

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledWith(
      'https://discord.com/api/v10/applications/123456789012345678/commands',
      {
        method: 'PUT',
        headers: {
          Authorization: 'Bot test-token',
          'Content-Type': 'application/json',
        },
        body: JSON.stringify([ping.definition, conch.definition]),
      },
    );
    expect(console.log).toHaveBeenCalledWith('Synced global commands.');
  });

  it.each(['DISCORD_APPLICATION_ID', 'DISCORD_BOT_TOKEN'])(
    'rejects a missing %s before calling Discord',
    async (name) => {
      vi.stubEnv(name, undefined);

      await expect(import('../scripts/register-commands.ts')).rejects.toThrow(
        'Set DISCORD_APPLICATION_ID and DISCORD_BOT_TOKEN in .env.',
      );
      expect(fetchMock).not.toHaveBeenCalled();
    },
  );

  it('reports a failed Discord request', async () => {
    fetchMock.mockResolvedValue(new Response('Missing Access', { status: 403 }));

    await expect(import('../scripts/register-commands.ts')).rejects.toThrow(
      'Discord returned 403: Missing Access',
    );
    expect(console.log).not.toHaveBeenCalled();
  });
});
