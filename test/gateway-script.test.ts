import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';

const fetchMock = vi.fn<typeof fetch>();
const originalArgv = process.argv;
const status = { running: true, leaseExpiresAt: '2026-10-31T13:00:00.000Z' };

beforeEach(() => {
  vi.resetModules();
  fetchMock.mockReset();
  fetchMock.mockResolvedValue(Response.json(status));
  process.argv = ['node', 'scripts/gateway.ts', 'status'];
  vi.stubEnv('GATEWAY_URL', 'https://bot.example.com');
  vi.stubEnv('GATEWAY_CONTROL_TOKEN', 'test-control-token');
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'log').mockImplementation(() => {});
});

afterEach(() => {
  process.argv = originalArgv;
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('gateway control script', () => {
  it.each([
    ['start', 'POST'],
    ['stop', 'POST'],
    ['status', 'GET'],
  ])('sends %s to its authenticated endpoint', async (action, method) => {
    process.argv[2] = action;

    await import('../scripts/gateway.ts');

    expect(fetchMock).toHaveBeenCalledExactlyOnceWith(`https://bot.example.com/gateway/${action}`, {
      method,
      headers: { Authorization: 'Bearer test-control-token' },
      redirect: 'error',
      signal: expect.any(AbortSignal),
    });
    expect(console.log).toHaveBeenCalledExactlyOnceWith(JSON.stringify(status, null, 2));
  });

  it.each([undefined, 'restart', '../stop'])(
    'rejects an invalid action %s before making a request',
    async (action) => {
      process.argv = ['node', 'scripts/gateway.ts', ...(action === undefined ? [] : [action])];

      await expect(import('../scripts/gateway.ts')).rejects.toThrow('Usage:');
      expect(fetchMock).not.toHaveBeenCalled();
    },
  );

  it.each(['GATEWAY_URL', 'GATEWAY_CONTROL_TOKEN'])(
    'requires %s before making a request',
    async (name) => {
      vi.stubEnv(name, '');

      await expect(import('../scripts/gateway.ts')).rejects.toThrow(
        'Set GATEWAY_URL and GATEWAY_CONTROL_TOKEN in your environment file.',
      );
      expect(fetchMock).not.toHaveBeenCalled();
    },
  );

  it('reports a non-OK status without printing the response body', async () => {
    fetchMock.mockResolvedValue(new Response('secret: test-control-token', { status: 403 }));

    await expect(import('../scripts/gateway.ts')).rejects.toThrow(/^Gateway returned HTTP 403\.$/);
    expect(console.log).not.toHaveBeenCalled();
  });
});
