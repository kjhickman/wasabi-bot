import { tracing } from 'cloudflare:workers';
import {
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
  Routes,
  type APIApplicationCommandAutocompleteInteraction,
  type APIApplicationCommandInteraction,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteraction,
  type APIInteractionResponse,
  type APIMessageApplicationCommandInteractionData,
} from 'discord-api-types/v10';
import { CommandError, reply, type CommandResponse, type Invocation } from '../commands/command.ts';
import { commandName, getCommand, runCommand } from '../commands/index.ts';
import { discordFetch, responseDetails, snowflakeTime, withFiles } from '../discord.ts';
import type { Env } from '../env.ts';
import { log, preview } from '../log.ts';
import { claim } from '../storage.ts';
import { takeUsage } from '../usage.ts';

const RESPONSE_WINDOW_MS = 3_000;
const CALLBACK_BUDGET_MS = 750;
const CALLBACK_TIMEOUT_MS = 10_000;
const DEFERRED_BUDGET_MS = 120_000;

type Outcome = 'ok' | 'command_error' | 'error';

// Each interaction gets a span, which groups its requests to Discord with the command's own
// span, named like `interaction /radio play`.
export async function handleInteraction(interaction: APIInteraction, env: Env, sql: SqlStorage) {
  if (
    interaction.type !== InteractionType.ApplicationCommand &&
    interaction.type !== InteractionType.ApplicationCommandAutocomplete
  ) {
    return;
  }
  const name = commandName(interaction.data);
  const kind =
    interaction.type === InteractionType.ApplicationCommand ? 'interaction' : 'autocomplete';
  return tracing.enterSpan(`${kind} ${name}`, (span) => answer(interaction, name, env, sql, span));
}

async function answer(
  interaction: APIApplicationCommandInteraction | APIApplicationCommandAutocompleteInteraction,
  name: string,
  env: Env,
  sql: SqlStorage,
  span: Span,
) {
  const user = interaction.member?.user ?? interaction.user;
  const context = {
    command: name,
    interactionId: interaction.id,
    guildId: interaction.guild_id,
    userId: user?.id,
  };
  // Discord allows 3 seconds from creation (encoded in the snowflake ID) to reply, and
  // delivering the interaction uses some of them.
  const createdAt = snowflakeTime(interaction.id);
  const deadline = createdAt + RESPONSE_WINDOW_MS;
  const ageMs = Date.now() - createdAt;
  span.setAttributes({ ...context, ageMs });
  if (deadline <= Date.now()) {
    span.setAttribute('outcome', 'expired');
    log.warn('Interaction arrived too late to answer', { ...context, ageMs });
    return;
  }
  if (!claim(sql, interaction.id)) {
    span.setAttribute('outcome', 'duplicate');
    return;
  }

  const callback = async (response: APIInteractionResponse) => {
    const msLeft = deadline - Date.now();
    if (msLeft <= 50) {
      log.warn('No time left to answer the interaction', { ...context, msLeft });
      return false;
    }
    const sent = await discordFetch(Routes.interactionCallback(interaction.id, interaction.token), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(response),
      // Discord rejects a reply that misses the window, but can take longer than the window
      // to accept one, so only cut off a request that hangs.
      signal: AbortSignal.timeout(CALLBACK_TIMEOUT_MS),
    });
    if (!sent.ok) {
      log.error('Discord refused the interaction reply', {
        ...context,
        msLeft,
        ...(await responseDetails(sent)),
      });
    }
    return sent.ok;
  };

  if (interaction.type === InteractionType.ApplicationCommandAutocomplete) {
    const sent = await callback({
      type: InteractionResponseType.ApplicationCommandAutocompleteResult,
      data: { choices: await suggest(interaction, env, deadline) },
    });
    span.setAttribute('outcome', sent ? 'ok' : 'error');
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
  const use = command?.limit && user ? takeUsage(sql, command.limit, user.id) : undefined;
  if (use && !use.ok) {
    span.setAttribute('outcome', 'limited');
    await callback(ephemeral(use.reason));
    return;
  }

  // A deferred reply's visibility is fixed when it is acknowledged, so flags can't change.
  const edit = async ({ files = [], data: { flags: _flags, ...message } }: CommandResponse) => {
    const edited = await discordFetch(
      Routes.webhookMessage(interaction.application_id, interaction.token, '@original'),
      { method: 'PATCH', body: withFiles(message, files) },
    );
    if (!edited.ok) {
      log.error('Discord refused the interaction edit', {
        ...context,
        ...(await responseDetails(edited)),
      });
    }
    return edited.ok;
  };

  // Deferred commands acknowledge up front. Others may defer themselves partway through.
  const respond = async (): Promise<Outcome> => {
    let deferred = false;
    const acknowledge = async () => {
      deferred ||= await callback({
        type: InteractionResponseType.DeferredChannelMessageWithSource,
      });
      return deferred;
    };
    if (command?.deferred && !(await acknowledge())) return 'error';
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
    span.setAttribute('deferred', deferred);
    const sent = deferred ? await edit(response) : await callback(response);
    return !sent ? 'error' : failed ? 'command_error' : 'ok';
  };

  const outcome = await respond().catch((error: unknown): Outcome => {
    const input = 'options' in data ? preview(JSON.stringify(data.options)) : undefined;
    log.error('Command failed', { ...context, error, input });
    return 'error';
  });
  span.setAttribute('outcome', outcome);
  // Only uses that reach the user without an error count toward the limit.
  if (outcome !== 'ok') use?.refund();
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
      log.error('Autocomplete failed', { command: name, error });
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
