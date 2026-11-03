import {
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
  Routes,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteraction,
  type APIInteractionResponse,
  type APIMessageApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import { CommandError, reply, type CommandResponse } from '../commands/command.ts';
import { getCommand, runCommand } from '../commands/index.ts';
import { discordFetch, snowflakeTime, withFiles } from '../discord.ts';
import type { Env } from '../env.ts';
import { claim } from '../storage.ts';
import { takeUsage } from '../usage.ts';

const RESPONSE_WINDOW_MS = 3_000;
const CALLBACK_BUDGET_MS = 750;
const DEFERRED_BUDGET_MS = 120_000;

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
  const deadline = snowflakeTime(interaction.id) + RESPONSE_WINDOW_MS;
  if (deadline <= Date.now() || !claim(sql, interaction.id)) return;

  const callback = async (response: APIInteractionResponse) => {
    const callbackBudget = Math.min(CALLBACK_BUDGET_MS, deadline - Date.now() - 50);
    if (callbackBudget <= 0) return false;
    const sent = await discordFetch(Routes.interactionCallback(interaction.id, interaction.token), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(response),
      signal: AbortSignal.timeout(callbackBudget),
    });
    if (!sent.ok) console.error('Discord interaction callback failed', sent.status);
    return sent.ok;
  };

  const command = getCommand(interaction.data.name);
  const user = interaction.member?.user ?? interaction.user;
  const use = command?.limit && user ? takeUsage(sql, command.limit, user.id) : undefined;
  if (use && 'refused' in use) {
    await callback(ephemeral(use.refused));
    return;
  }

  // Only uses that reach the user without an error count toward the limit.
  let succeeded = false;
  try {
    if (command?.deferred) {
      if (!(await callback({ type: InteractionResponseType.DeferredChannelMessageWithSource }))) {
        return;
      }
      const { response, failed } = await run(
        interaction.data,
        env,
        AbortSignal.timeout(DEFERRED_BUDGET_MS),
      );
      // A deferred reply's visibility is fixed when it is acknowledged, so flags can't change.
      const {
        files = [],
        data: { flags: _flags, ...message },
      } = response;
      const edited = await discordFetch(
        Routes.webhookMessage(interaction.application_id, interaction.token, '@original'),
        { method: 'PATCH', body: withFiles(message, files) },
      );
      if (!edited.ok) console.error('Discord interaction edit failed', edited.status);
      succeeded = edited.ok && !failed;
      return;
    }

    const commandBudget = deadline - Date.now() - CALLBACK_BUDGET_MS;
    const signal = commandBudget > 0 ? AbortSignal.timeout(commandBudget) : AbortSignal.abort();
    const { response, failed } = await run(interaction.data, env, signal);
    succeeded = (await callback(response)) && !failed;
  } catch {
    console.error('Gateway command failed');
  } finally {
    if (!succeeded) use?.refund();
  }
}

// A command's own errors are shown privately to the person who ran it.
async function run(
  data: APIChatInputApplicationCommandInteractionData | APIMessageApplicationCommandInteractionData,
  env: Env,
  signal: AbortSignal,
): Promise<{ response: CommandResponse; failed: boolean }> {
  try {
    return { response: await runCommand(data, env, signal), failed: false };
  } catch (error) {
    if (!(error instanceof CommandError)) throw error;
    return { response: ephemeral(error.message), failed: true };
  }
}

function ephemeral(content: string) {
  return reply({ content, flags: MessageFlags.Ephemeral });
}
