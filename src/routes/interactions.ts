import {
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
  type APIInteraction,
} from 'discord-api-types/payloads/v10';
import { Hono } from 'hono';
import { bodyLimit } from 'hono/body-limit';
import { ping } from '../commands/ping.ts';
import { verifyDiscordRequest } from '../discord.ts';
import type { AppEnv } from '../env.ts';

export const interactions = new Hono<AppEnv>();

interactions.post('/', bodyLimit({ maxSize: 1024 * 1024 }), async (c) => {
  const body = await c.req.text();
  if (!(await verifyDiscordRequest(c.req.raw, body, c.env.DISCORD_PUBLIC_KEY))) {
    return c.text('Invalid request signature', 401);
  }

  let interaction: APIInteraction;
  try {
    interaction = JSON.parse(body) as APIInteraction;
  } catch {
    return c.text('Invalid JSON', 400);
  }

  if (!interaction || typeof interaction !== 'object') {
    return c.text('Invalid interaction', 400);
  }

  if (interaction.type === InteractionType.Ping) {
    return c.json({ type: InteractionResponseType.Pong });
  }

  if (
    interaction.type !== InteractionType.ApplicationCommand ||
    interaction.data?.type !== ApplicationCommandType.ChatInput
  ) {
    return c.text('Unsupported interaction', 400);
  }

  if (interaction.data.name === ping.definition.name) {
    return c.json(ping.execute());
  }

  return c.json({
    type: InteractionResponseType.ChannelMessageWithSource,
    data: { content: 'Unknown command.', flags: MessageFlags.Ephemeral },
  });
});

interactions.all('/', (c) => c.text('Method not allowed', 405, { Allow: 'POST' }));
