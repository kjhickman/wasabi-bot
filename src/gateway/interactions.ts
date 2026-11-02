import {
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  RouteBases,
  Routes,
  type APIInteraction,
  type APIInteractionResponse,
} from 'discord-api-types/v10';
import { isDeferred, runCommand, withFiles } from '../commands/index.ts';
import type { Env } from '../env.ts';

const DISCORD_EPOCH_MS = 1_420_070_400_000;
const RESPONSE_WINDOW_MS = 3_000;
const CALLBACK_BUDGET_MS = 750;
const DEFERRED_BUDGET_MS = 120_000;
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
    (interaction.data.type !== ApplicationCommandType.ChatInput &&
      interaction.data.type !== ApplicationCommandType.Message)
  ) {
    return;
  }
  const deadline = Number(BigInt(interaction.id) >> 22n) + DISCORD_EPOCH_MS + RESPONSE_WINDOW_MS;
  if (deadline <= Date.now() || !claim(sql, interaction.id)) return;

  const callback = async (response: APIInteractionResponse) => {
    const callbackBudget = Math.min(CALLBACK_BUDGET_MS, deadline - Date.now() - 50);
    if (callbackBudget <= 0) return false;
    const sent = await fetch(
      `${RouteBases.api}${Routes.interactionCallback(interaction.id, interaction.token)}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(response),
        redirect: 'manual',
        signal: AbortSignal.timeout(callbackBudget),
      },
    );
    if (!sent.ok) console.error('Discord interaction callback failed', sent.status);
    return sent.ok;
  };

  try {
    if (isDeferred(interaction.data.name)) {
      if (!(await callback({ type: InteractionResponseType.DeferredChannelMessageWithSource }))) {
        return;
      }
      const response = await runCommand(
        interaction.data,
        env,
        AbortSignal.timeout(DEFERRED_BUDGET_MS),
      );
      // A deferred reply's visibility is fixed when it is acknowledged, so flags can't change.
      const {
        files = [],
        data: { flags: _, ...message },
      } = response;
      const edited = await fetch(
        `${RouteBases.api}${Routes.webhookMessage(interaction.application_id, interaction.token, '@original')}`,
        { method: 'PATCH', body: withFiles(message, files), redirect: 'manual' },
      );
      if (!edited.ok) console.error('Discord interaction edit failed', edited.status);
      return;
    }

    const commandBudget = deadline - Date.now() - CALLBACK_BUDGET_MS;
    const signal = commandBudget > 0 ? AbortSignal.timeout(commandBudget) : AbortSignal.abort();
    await callback(await runCommand(interaction.data, env, signal));
  } catch {
    console.error('Gateway command failed');
  }
}

export function claim(sql: SqlStorage, id: string) {
  const inserted = sql
    .exec(
      'INSERT OR IGNORE INTO interactions (id, expires_at) VALUES (?, ?) RETURNING id',
      id,
      Date.now() + SEEN_TTL_MS,
    )
    .toArray();
  return inserted.length > 0;
}
