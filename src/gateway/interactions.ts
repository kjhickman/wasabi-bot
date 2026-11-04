import {
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
  Routes,
  type APIApplicationCommandAutocompleteInteraction,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteraction,
  type APIInteractionResponse,
  type APIMessageApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import { CommandError, reply, type CommandResponse, type Invocation } from '../commands/command.ts';
import { getCommand, runCommand } from '../commands/index.ts';
import { discordFetch, snowflakeTime, withFiles } from '../discord.ts';
import type { Env } from '../env.ts';
import { errorMessage } from '../errors.ts';
import { claim } from '../storage.ts';
import { takeUsage } from '../usage.ts';

const RESPONSE_WINDOW_MS = 3_000;
const CALLBACK_BUDGET_MS = 750;
const DEFERRED_BUDGET_MS = 120_000;

// Discord allows 3 seconds from creation (encoded in the snowflake ID) to reply.
export async function handleInteraction(interaction: APIInteraction, env: Env, sql: SqlStorage) {
  if (
    interaction.type !== InteractionType.ApplicationCommand &&
    interaction.type !== InteractionType.ApplicationCommandAutocomplete
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

  if (interaction.type === InteractionType.ApplicationCommandAutocomplete) {
    await callback({
      type: InteractionResponseType.ApplicationCommandAutocompleteResult,
      data: { choices: await suggest(interaction, env, deadline) },
    });
    return;
  }
  const { data } = interaction;
  if (
    data.type !== ApplicationCommandType.ChatInput &&
    data.type !== ApplicationCommandType.Message
  ) {
    return;
  }

  const command = getCommand(data.name);
  const user = interaction.member?.user ?? interaction.user;
  const use = command?.limit && user ? takeUsage(sql, command.limit, user.id) : undefined;
  if (use && !use.ok) {
    await callback(ephemeral(use.reason));
    return;
  }

  // Deferred commands acknowledge up front. Others may defer themselves partway through.
  const respond = async () => {
    let deferred = false;
    const acknowledge = async () => {
      deferred ||= await callback({
        type: InteractionResponseType.DeferredChannelMessageWithSource,
      });
      return deferred;
    };
    if (command?.deferred && !(await acknowledge())) return false;
    const invocation: Invocation = {
      guildId: interaction.guild_id,
      userId: user?.id,
      defer: async () => {
        if (!(await acknowledge())) throw new Error('Discord did not accept the deferral');
      },
    };

    const commandBudget = deadline - Date.now() - CALLBACK_BUDGET_MS;
    const signal = deferred
      ? AbortSignal.timeout(DEFERRED_BUDGET_MS)
      : commandBudget > 0
        ? AbortSignal.timeout(commandBudget)
        : AbortSignal.abort();
    const { response, failed } = await run(data, env, signal, invocation);
    if (!deferred) return (await callback(response)) && !failed;

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
    return edited.ok && !failed;
  };

  // Only uses that reach the user without an error count toward the limit.
  const counted = await respond().catch((error: unknown) => {
    console.error('Gateway command failed', data.name, errorMessage(error));
    return false;
  });
  if (!counted) use?.refund();
}

// Discord drops suggestions that arrive late, so a slow lookup suggests nothing.
async function suggest(
  interaction: APIApplicationCommandAutocompleteInteraction,
  env: Env,
  deadline: number,
) {
  const { name } = interaction.data;
  const command = getCommand(name);
  const budget = deadline - Date.now() - CALLBACK_BUDGET_MS;
  if (!command?.autocomplete || budget <= 0) return [];
  return command
    .autocomplete(interaction.data, env, AbortSignal.timeout(budget))
    .catch((error: unknown) => {
      console.error('Autocomplete failed', name, errorMessage(error));
      return [];
    });
}

async function run(
  data: APIChatInputApplicationCommandInteractionData | APIMessageApplicationCommandInteractionData,
  env: Env,
  signal: AbortSignal,
  invocation: Invocation,
): Promise<{ response: CommandResponse; failed: boolean }> {
  try {
    return { response: await runCommand(data, env, signal, invocation), failed: false };
  } catch (error) {
    if (!(error instanceof CommandError)) throw error;
    return { response: ephemeral(error.message), failed: true };
  }
}

function ephemeral(content: string) {
  return reply({ content, flags: MessageFlags.Ephemeral });
}
