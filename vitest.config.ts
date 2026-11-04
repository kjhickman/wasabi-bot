import { defineConfig } from 'vite-plus';

export default defineConfig({
  resolve: {
    alias: {
      'cloudflare:workers': new URL('test/fixtures/cloudflare-workers.ts', import.meta.url)
        .pathname,
    },
  },
  test: {
    include: ['test/**/*.test.ts'],
  },
});
