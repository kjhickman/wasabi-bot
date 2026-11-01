import {
  ApplicationCommandType,
  InteractionType,
  type APIInteraction,
} from 'discord-api-types/v10';
import { runCommand } from '../commands/index.ts';
import type { Env } from '../env.ts';

const DISCORD_EPOCH_MS = 1_420_070_400_000;
const RESPONSE_WINDOW_MS = 3_000;
const CALLBACK_BUDGET_MS = 750;
const SEEN_TTL_MS = 15 * 60 * 1_000;

export function createInteractionsTable(sql: SqlStorage) {
  sql.exec(
    'CREATE TABLE IF NOT EXISTS interactions (id TEXT PRIMARY KEY, expires_at INTEGER NOT NULL)',
  );
}

export function pruneInteractions(sql: SqlStorage) {
  sql.exec('DELETE FROM interactions WHERE expires_at <= ?', Date.now());
}

// Discord allows 3 seconds from creation (encoded in the snowflake ID) to reply. Each
// interaction is claimed once so a replayed event never runs a command twice.
export async function handleInteraction(interaction: APIInteraction, env: Env, sql: SqlStorage) {
  if (
    interaction.type !== InteractionType.ApplicationCommand ||
    interaction.data.type !== ApplicationCommandType.ChatInput
  ) {
    return;
  }
  const deadline = Number(BigInt(interaction.id) >> 22n) + DISCORD_EPOCH_MS + RESPONSE_WINDOW_MS;
  if (deadline <= Date.now() || !claim(sql, interaction.id)) return;

  try {
    const commandBudget = deadline - Date.now() - CALLBACK_BUDGET_MS;
    const signal = commandBudget > 0 ? AbortSignal.timeout(commandBudget) : AbortSignal.abort();
    const response = await runCommand(interaction.data, env, signal);
    const callbackBudget = Math.min(CALLBACK_BUDGET_MS, deadline - Date.now() - 50);
    if (callbackBudget <= 0) return;
    const sent = await fetch(
      `https://discord.com/api/v10/interactions/${interaction.id}/${interaction.token}/callback`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(response),
        redirect: 'manual',
        signal: AbortSignal.timeout(callbackBudget),
      },
    );
    if (!sent.ok) console.error('Discord interaction callback failed', sent.status);
  } catch {
    console.error('Gateway command failed');
  }
}

function claim(sql: SqlStorage, id: string) {
  const inserted = sql
    .exec(
      'INSERT OR IGNORE INTO interactions (id, expires_at) VALUES (?, ?) RETURNING id',
      id,
      Date.now() + SEEN_TTL_MS,
    )
    .toArray();
  return inserted.length > 0;
}
