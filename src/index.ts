import { Hono } from 'hono';
import type { AppEnv } from './env.ts';
import { interactions } from './routes/interactions.ts';

const app = new Hono<AppEnv>();

app.route('/interactions', interactions);
app.notFound((c) => c.text('Not found', 404));

export default app;
