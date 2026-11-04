import { tracing } from 'cloudflare:workers';
import {
  MessageFlags,
  Routes,
  type APIMessage,
  type APIUser,
  type GatewayMessageCreateDispatchData,
  type RESTPostAPIChannelMessageJSONBody,
} from 'discord-api-types/v10';
import OpenAI from 'openai';
import type { ResponseInputItem, Tool } from 'openai/resources/responses/responses';
import { systemPrompt } from './prompt.ts';
import { chatTools, commandLabel, runTool, type ToolContext } from './tools.ts';
import {
  discordFetch,
  MAX_MESSAGE_LENGTH,
  responseDetails,
  snowflakeTime,
  withFiles,
} from '../discord.ts';
import type { Env } from '../env.ts';
import { log, preview } from '../log.ts';
import { createResponse, MODELS } from '../openai.ts';
import { takeUsage } from '../usage.ts';

const HISTORY_LIMIT = 50;
const MAX_TOOL_ROUNDS = 3;
const COMMAND_TIMEOUT_MS = 120_000;
const REPLY_TIMEOUT_MS = 180_000;
const TYPING_INTERVAL_MS = 8_000;
const FAILURE_REPLY = "Sorry, I couldn't come up with a reply. Try again in a moment.";
const TOOLS: Tool[] = [
  ...chatTools,
  { type: 'web_search' },
  { type: 'code_interpreter', container: { type: 'auto' } },
];

type Message = Pick<APIMessage, 'author' | 'content' | 'mentions'>;

export interface ChatOptions {
  bot: APIUser;
  token: string;
  env: Env;
  sql: SqlStorage;
  signal?: AbortSignal;
}

interface Conversation extends ToolContext {
  bot: APIUser;
  dm: boolean;
  typing(): Promise<unknown>;
}

export function addressesBot(message: GatewayMessageCreateDispatchData, bot: APIUser) {
  const isDM = !message.guild_id;
  return !message.author.bot && (isDM || message.mentions.some((user) => user.id === bot.id));
}

export function replyToMessage(message: GatewayMessageCreateDispatchData, options: ChatOptions) {
  return tracing.enterSpan('chat', (span) => reply(message, options, span));
}

async function reply(
  message: GatewayMessageCreateDispatchData,
  { bot, token, env, sql, signal = AbortSignal.timeout(REPLY_TIMEOUT_MS) }: ChatOptions,
  span: Span,
) {
  const dm = !message.guild_id;
  const context = {
    messageId: message.id,
    channelId: message.channel_id,
    guildId: message.guild_id,
    userId: message.author.id,
  };
  span.setAttributes({ ...context, dm, ageMs: Date.now() - snowflakeTime(message.id) });
  const messagesRoute = Routes.channelMessages(message.channel_id);
  const typing = () =>
    discordFetch(Routes.channelTyping(message.channel_id), { method: 'POST', token }).catch(
      () => undefined,
    );
  const send = async (content: string, files: File[] = []) => {
    const body = {
      content,
      ...(!dm && { message_reference: { message_id: message.id, fail_if_not_exists: false } }),
      allowed_mentions: { parse: [], replied_user: true },
      flags: MessageFlags.SuppressEmbeds,
    } satisfies RESTPostAPIChannelMessageJSONBody;
    const sent = await discordFetch(messagesRoute, {
      method: 'POST',
      body: withFiles(body, files),
      token,
    });
    if (!sent.ok) {
      log.error('Discord refused the chat reply', { ...context, ...(await responseDetails(sent)) });
    }
    return sent.ok;
  };

  const userId = message.author.id;
  const use = takeUsage(sql, 'text', userId);
  if (!use.ok) {
    span.setAttribute('outcome', 'limited');
    await send(use.reason);
    return;
  }

  const compose = async () => {
    const [, response] = await Promise.all([
      typing(),
      discordFetch(`${messagesRoute}?before=${message.id}&limit=${HISTORY_LIMIT - 1}`, {
        signal,
        token,
      }),
    ]);
    if (!response.ok) {
      log.warn('Chat history unavailable', { ...context, ...(await responseDetails(response)) });
    }
    const recent = response.ok ? await response.json<APIMessage[]>() : [];
    const image = [message, message.referenced_message, ...recent]
      .flatMap((source) => source?.attachments ?? [])
      .find((attachment) => attachment.content_type?.startsWith('image/'));
    span.setAttributes({ history: recent.length, image: image !== undefined });
    return generate([...recent.toReversed(), message], {
      bot,
      dm,
      typing,
      env,
      sql,
      userId,
      guildId: message.guild_id,
      image,
      signal,
    });
  };

  const composed = await compose().catch((error: unknown) => {
    use.refund();
    log.error('Chat reply failed', { ...context, error, input: preview(message.content) });
    return undefined;
  });
  const sent = await send(composed?.content ?? FAILURE_REPLY, composed?.files);
  span.setAttribute('outcome', composed && sent ? 'ok' : 'error');
}

async function generate(messages: Message[], conversation: Conversation) {
  const { bot, dm, typing, env, signal } = conversation;
  if (!env.OPENAI_API_KEY) throw new Error('OPENAI_API_KEY is not set');
  const instructions = systemPrompt(displayName(bot), dm);
  const client = new OpenAI({ apiKey: env.OPENAI_API_KEY, timeout: 30_000 });
  const input = messages.flatMap((message): ResponseInputItem[] => {
    const text = readable(message);
    if (!text) return [];
    return message.author.id === bot.id
      ? [{ role: 'assistant', content: text }]
      : [{ role: 'user', content: `${displayName(message.author)}: ${text}` }];
  });
  const used = new Set<string>();
  const files: File[] = [];
  // The model sometimes says nothing after a command that did the job, like starting the
  // radio, so the command's own result stands in for its reply.
  let lastResult: string | undefined;

  for (let round = 0; round <= MAX_TOOL_ROUNDS; round++) {
    const response = await createResponse(
      client,
      {
        model: MODELS.text,
        instructions,
        input,
        tools: TOOLS,
        tool_choice: round < MAX_TOOL_ROUNDS ? 'auto' : 'none',
        reasoning: { effort: 'none' },
        max_output_tokens: 500,
      },
      signal,
    );
    const calls = response.output.filter((item) => item.type === 'function_call');
    if (!calls.length) {
      const text = response.output_text.trim() || (files.length ? '' : (lastResult ?? ''));
      if (!text && !files.length) {
        const reason = response.incomplete_details?.reason;
        const output = response.output.map((item) => item.type).join(', ') || 'nothing';
        throw new Error(
          `OpenAI returned no text (status ${response.status}${reason ? `: ${reason}` : ''}; output: ${output})`,
        );
      }
      return { content: withNote(text, used), files };
    }

    void typing();
    const keepTyping = setInterval(() => void typing(), TYPING_INTERVAL_MS);
    const outputs = await Promise.all(
      calls.map(async (call): Promise<ResponseInputItem> => {
        const label = commandLabel(call.name);
        if (label) used.add(label);
        const deadline = AbortSignal.any([signal, AbortSignal.timeout(COMMAND_TIMEOUT_MS)]);
        const result = await runTool(call.name, call.arguments, {
          ...conversation,
          signal: deadline,
        });
        files.push(...result.files);
        if (label) lastResult = result.output;
        return { type: 'function_call_output', call_id: call.call_id, output: result.output };
      }),
    ).finally(() => clearInterval(keepTyping));
    const toolCalls = response.output.filter(
      (item) =>
        item.type === 'function_call' ||
        item.type === 'web_search_call' ||
        item.type === 'code_interpreter_call',
    );
    input.push(...toolCalls, ...outputs);
  }
  throw new Error('OpenAI kept calling tools');
}

function withNote(text: string, used: Set<string>) {
  const note = used.size ? `\n-# used ${[...used].map((name) => `/${name}`).join(', ')}` : '';
  const limit = MAX_MESSAGE_LENGTH - note.length;
  return ((text.length > limit ? `${text.slice(0, limit - 1)}…` : text) + note).trim();
}

function readable(message: Message) {
  return message.content
    .replace(/<@!?(\d+)>/g, (mention, id: string) => {
      const user = message.mentions.find((user) => user.id === id);
      return user ? `@${displayName(user)}` : mention;
    })
    .trim();
}

function displayName(user: APIUser) {
  return user.global_name ?? user.username;
}
