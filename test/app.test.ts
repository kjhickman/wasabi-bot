import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import app from '../src/app.ts';
import type { Env } from '../src/env.ts';

const stub = {
  status: vi.fn(async () => ({ running: false })),
  start: vi.fn(async () => ({ running: true })),
  stop: vi.fn(async () => ({ running: false })),
};
const getByName = vi.fn(() => stub);
const env = {
  GATEWAY_MODE: 'dev',
  GATEWAY_CONTROL_TOKEN: 'test-token',
  DISCORD_GATEWAY: { getByName },
} as unknown as Env;

beforeEach(() => vi.clearAllMocks());
afterEach(() => vi.restoreAllMocks());

describe('Gateway controls', () => {
  it.each([
    ['status', 'GET', false],
    ['start', 'POST', true],
    ['stop', 'POST', false],
  ] as const)('authenticates %s and addresses the singleton', async (action, method, running) => {
    const response = await app.fetch(
      new Request(`https://bot.test/gateway/${action}`, {
        method,
        headers: { Authorization: 'Bearer test-token' },
      }),
      env,
    );

    expect(response.status).toBe(200);
    expect(response.headers.get('Cache-Control')).toBe('no-store');
    expect(await response.json()).toEqual({ running });
    expect(getByName).toHaveBeenCalledExactlyOnceWith('gateway');
    expect(stub[action]).toHaveBeenCalledOnce();
  });

  it.each([undefined, 'Bearer wrong-token'])('rejects %s without waking the DO', async (token) => {
    const response = await app.fetch(
      new Request('https://bot.test/gateway/start', {
        method: 'POST',
        headers: token ? { Authorization: token } : {},
      }),
      env,
    );

    expect(response.status).toBe(401);
    expect(getByName).not.toHaveBeenCalled();
  });

  it('fails closed if the admin secret is missing', async () => {
    const { GATEWAY_CONTROL_TOKEN: _token, ...unconfigured } = env;
    const response = await app.fetch(new Request('https://bot.test/gateway/status'), unconfigured);

    expect(response.status).toBe(503);
    expect(getByName).not.toHaveBeenCalled();
  });

  it('does not start the Gateway from a GET', async () => {
    const response = await app.fetch(
      new Request('https://bot.test/gateway/start', {
        headers: { Authorization: 'Bearer test-token' },
      }),
      env,
    );

    expect(response.status).toBe(404);
    expect(getByName).not.toHaveBeenCalled();
  });
});
