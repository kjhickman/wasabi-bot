const action = process.argv[2];

if (action !== 'start' && action !== 'stop' && action !== 'status') {
  throw new Error('Usage: node --env-file=.env scripts/gateway.ts <start|stop|status>');
}

const { GATEWAY_URL, GATEWAY_CONTROL_TOKEN } = process.env;

if (!GATEWAY_URL || !GATEWAY_CONTROL_TOKEN) {
  throw new Error('Set GATEWAY_URL and GATEWAY_CONTROL_TOKEN in your environment file.');
}

const response = await fetch(new URL(`/gateway/${action}`, GATEWAY_URL).href, {
  method: action === 'status' ? 'GET' : 'POST',
  headers: { Authorization: `Bearer ${GATEWAY_CONTROL_TOKEN}` },
  redirect: 'error',
  signal: AbortSignal.timeout(10_000),
});

if (!response.ok) {
  throw new Error(`Gateway returned HTTP ${response.status}.`);
}

console.log(JSON.stringify(await response.json(), null, 2));
