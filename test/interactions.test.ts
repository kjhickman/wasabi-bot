import { webcrypto } from 'node:crypto';
import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
} from 'discord-api-types/v10';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vite-plus/test';
import worker from '../src/index.ts';

let keyPair: webcrypto.CryptoKeyPair;
let publicKey: string;

beforeAll(async () => {
  keyPair = (await webcrypto.subtle.generateKey('Ed25519', true, [
    'sign',
    'verify',
  ])) as webcrypto.CryptoKeyPair;
  publicKey = Buffer.from(await webcrypto.subtle.exportKey('raw', keyPair.publicKey)).toString(
    'hex',
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
});

async function signedRequest(
  body: string,
  timestamp = String(Math.floor(Date.now() / 1000)),
): Promise<Request> {
  const signature = await webcrypto.subtle.sign(
    'Ed25519',
    keyPair.privateKey,
    new TextEncoder().encode(timestamp + body),
  );

  return new Request('https://bot.example/interactions', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Signature-Ed25519': Buffer.from(signature).toString('hex'),
      'X-Signature-Timestamp': timestamp,
    },
    body,
  });
}

async function handle(request: Request): Promise<Response> {
  return worker.fetch(request, { DISCORD_PUBLIC_KEY: publicKey });
}

describe('interactions endpoint', () => {
  it('acknowledges Discord verification using the original request body', async () => {
    const response = await handle(await signedRequest('{ "type": 1 }'));

    expect(response.status).toBe(200);
    expect(response.headers.get('Content-Type')).toContain('application/json');
    expect(await response.json()).toEqual({ type: InteractionResponseType.Pong });
  });

  it('responds to /ping', async () => {
    const response = await handle(
      await signedRequest(
        JSON.stringify({
          type: InteractionType.ApplicationCommand,
          data: { type: ApplicationCommandType.ChatInput, name: 'ping' },
        }),
      ),
    );

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Pong!' },
    });
  });

  it('responds directly to /conch using the Decisions API', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(Response.json({ answers: [{ type: 'choice', choice: 'yes' }] }));
    vi.stubGlobal('fetch', fetchMock);
    const request = await signedRequest(
      JSON.stringify({
        type: InteractionType.ApplicationCommand,
        data: {
          type: ApplicationCommandType.ChatInput,
          name: 'conch',
          options: [
            {
              type: ApplicationCommandOptionType.String,
              name: 'question',
              value: 'Is the sky blue?',
            },
          ],
        },
      }),
    );

    const response = await worker.fetch(request, {
      DISCORD_PUBLIC_KEY: publicKey,
      OPENAI_API_KEY: 'test-key',
    });

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Yes' },
    });
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('does not call OpenAI for an unverified /conch interaction', async () => {
    const fetchMock = vi.fn<typeof fetch>();
    vi.stubGlobal('fetch', fetchMock);
    const request = await signedRequest(
      JSON.stringify({
        type: InteractionType.ApplicationCommand,
        data: { type: ApplicationCommandType.ChatInput, name: 'conch' },
      }),
    );
    request.headers.delete('X-Signature-Ed25519');

    expect((await handle(request)).status).toBe(401);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([false, true])(
    'rejects oversized unsigned bodies (Content-Length: %s)',
    async (includeContentLength) => {
      const body = 'x'.repeat(1024 * 1024 + 1);
      const request = new Request('https://bot.example/interactions', {
        method: 'POST',
        headers: includeContentLength ? { 'Content-Length': String(body.length) } : {},
        body,
      });

      expect((await handle(request)).status).toBe(413);
      if (includeContentLength) {
        expect(request.bodyUsed).toBe(false);
      }
    },
  );

  it.each([false, true])(
    'accepts signed bodies at the size limit (Content-Length: %s)',
    async (includeContentLength) => {
      const body = '{"type":1}'.padEnd(1024 * 1024, ' ');
      const request = await signedRequest(body);
      if (includeContentLength) {
        request.headers.set('Content-Length', String(body.length));
      }

      const response = await handle(request);

      expect(response.status).toBe(200);
      expect(await response.json()).toEqual({ type: InteractionResponseType.Pong });
    },
  );

  it.each(['X-Signature-Ed25519', 'X-Signature-Timestamp'])(
    'rejects a missing %s',
    async (header) => {
      const request = await signedRequest('{"type":1}');
      request.headers.delete(header);

      expect((await handle(request)).status).toBe(401);
    },
  );

  it.each(['not-hex', 'ff', '00'.repeat(64)])(
    'rejects an invalid signature: %s',
    async (signature) => {
      const request = await signedRequest('{"type":1}');
      request.headers.set('X-Signature-Ed25519', signature);

      expect((await handle(request)).status).toBe(401);
    },
  );

  it('rejects a tampered body', async () => {
    const request = await signedRequest('{"type":1}');
    const tampered = new Request(request, { method: 'POST', body: '{"type":2}' });

    expect((await handle(tampered)).status).toBe(401);
  });

  it('rejects a signature from another key', async () => {
    const otherKeyPair = (await webcrypto.subtle.generateKey('Ed25519', true, [
      'sign',
      'verify',
    ])) as webcrypto.CryptoKeyPair;
    const otherPublicKey = Buffer.from(
      await webcrypto.subtle.exportKey('raw', otherKeyPair.publicKey),
    ).toString('hex');
    const response = await worker.fetch(await signedRequest('{"type":1}'), {
      DISCORD_PUBLIC_KEY: otherPublicKey,
    });

    expect(response.status).toBe(401);
  });

  it.each(['', 'not-hex', 'ff'.repeat(32)])('rejects an invalid public key: %s', async (key) => {
    const response = await worker.fetch(await signedRequest('{"type":1}'), {
      DISCORD_PUBLIC_KEY: key,
    });

    expect(response.status).toBe(401);
  });

  it.each([-600, 600])('rejects a timestamp offset by %s seconds', async (offset) => {
    const timestamp = String(Math.floor(Date.now() / 1000) + offset);

    expect((await handle(await signedRequest('{"type":1}', timestamp))).status).toBe(401);
  });

  it('rejects a nonnumeric timestamp', async () => {
    expect((await handle(await signedRequest('{"type":1}', 'invalid'))).status).toBe(401);
  });

  it('verifies the signature before parsing JSON', async () => {
    const request = await signedRequest('invalid JSON');
    request.headers.set('X-Signature-Ed25519', '00'.repeat(64));

    expect((await handle(request)).status).toBe(401);
  });

  it.each(['invalid JSON', 'null'])('rejects a signed malformed payload: %s', async (body) => {
    expect((await handle(await signedRequest(body))).status).toBe(400);
  });

  it('returns an ephemeral response for an unknown command', async () => {
    const response = await handle(
      await signedRequest(
        JSON.stringify({
          type: InteractionType.ApplicationCommand,
          data: { type: ApplicationCommandType.ChatInput, name: 'unknown' },
        }),
      ),
    );

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
    });
  });

  it('rejects unsupported interactions', async () => {
    const request = await signedRequest(JSON.stringify({ type: InteractionType.MessageComponent }));

    expect((await handle(request)).status).toBe(400);
  });

  it.each(['/', '/missing', '/interactions/', '/interactions/missing', '/interactions-extra'])(
    'returns 404 for %s',
    async (path) => {
      const response = await handle(new Request(`https://bot.example${path}`));

      expect(response.status).toBe(404);
      expect(await response.text()).toBe('Not found');
    },
  );

  it.each(['GET', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'HEAD'])(
    'rejects %s requests',
    async (method) => {
      const response = await handle(new Request('https://bot.example/interactions', { method }));

      expect(response.status).toBe(405);
      expect(response.headers.get('Allow')).toBe('POST');
    },
  );
});
