import { readFileSync } from 'node:fs';
import type { AddressInfo } from 'node:net';
import { parseEnv } from 'node:util';
import { cloudflare } from '@cloudflare/vite-plugin';
import { defineConfig, type Plugin, type ViteDevServer } from 'vite-plus';

// Local dev has no cron, so connect the Gateway as soon as the dev server is listening.
const startGateway: Plugin = {
  name: 'start-gateway',
  apply: 'serve',
  configureServer(server) {
    server.httpServer?.once('listening', () => {
      start(server).catch((error) =>
        server.config.logger.error(`Gateway did not start: ${error.message}`),
      );
    });
  },
};

async function start(server: ViteDevServer) {
  const { port } = server.httpServer!.address() as AddressInfo;
  const { GATEWAY_CONTROL_TOKEN } = parseEnv(readFileSync('.dev.vars', 'utf8'));
  const response = await fetch(`http://localhost:${port}/gateway/start`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${GATEWAY_CONTROL_TOKEN}` },
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  server.config.logger.info('Gateway started.');
}

export default defineConfig({
  plugins: [cloudflare(), startGateway],
  fmt: {
    singleQuote: true,
    ignorePatterns: ['worker-configuration.d.ts'],
  },
  lint: {
    ignorePatterns: ['worker-configuration.d.ts'],
  },
});
