import { cloudflare } from '@cloudflare/vite-plugin';
import { defineConfig } from 'vite-plus';

export default defineConfig({
  plugins: [cloudflare()],
  fmt: {
    singleQuote: true,
    ignorePatterns: ['worker-configuration.d.ts'],
  },
  lint: {
    ignorePatterns: ['worker-configuration.d.ts'],
  },
});
