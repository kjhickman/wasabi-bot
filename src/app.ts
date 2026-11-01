import { Hono } from 'hono';
import { bearerAuth } from 'hono/bearer-auth';
import type { Env } from './env.ts';

type AppEnv = { Bindings: Env };

const app = new Hono<AppEnv>();

app.use('/gateway/*', async (c, next) => {
  if (!c.env.GATEWAY_CONTROL_TOKEN) {
    return c.text('Gateway controls are not configured.', 503);
  }
  c.header('Cache-Control', 'no-store');
  return bearerAuth<AppEnv>({ token: c.env.GATEWAY_CONTROL_TOKEN })(c, next);
});

app.get('/gateway/status', async (c) => c.json(await gateway(c.env).status()));
app.post('/gateway/start', async (c) => c.json(await gateway(c.env).start()));
app.post('/gateway/stop', async (c) => c.json(await gateway(c.env).stop()));
app.notFound((c) => c.text('Not found', 404));

function gateway(env: Env) {
  return env.DISCORD_GATEWAY.getByName('gateway');
}

export default app;
