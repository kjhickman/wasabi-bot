const MAX_REQUEST_AGE_SECONDS = 300;

export async function verifyDiscordRequest(
  request: Request,
  body: string,
  publicKey: string,
): Promise<boolean> {
  const signature = request.headers.get('X-Signature-Ed25519');
  const timestamp = request.headers.get('X-Signature-Timestamp');

  if (
    !signature ||
    !/^[\da-f]{128}$/i.test(signature) ||
    !timestamp ||
    !/^\d+$/.test(timestamp) ||
    !/^[\da-f]{64}$/i.test(publicKey)
  ) {
    return false;
  }

  const age = Math.abs(Date.now() / 1000 - Number(timestamp));
  if (age > MAX_REQUEST_AGE_SECONDS) {
    return false;
  }

  try {
    const key = await crypto.subtle.importKey('raw', fromHex(publicKey), 'Ed25519', false, [
      'verify',
    ]);

    return await crypto.subtle.verify(
      'Ed25519',
      key,
      fromHex(signature),
      new TextEncoder().encode(timestamp + body),
    );
  } catch {
    return false;
  }
}

function fromHex(hex: string): Uint8Array<ArrayBuffer> {
  return Uint8Array.from({ length: hex.length / 2 }, (_, index) =>
    Number.parseInt(hex.slice(index * 2, index * 2 + 2), 16),
  );
}
