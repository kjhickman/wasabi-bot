import {
  MessageFlags,
  RouteBases,
  Routes,
  type APIAttachment,
  type APIMessage,
  type APIUser,
  type GatewayMessageCreateDispatchData,
  type RESTPostAPIChannelMessageJSONBody,
} from 'discord-api-types/v10';
import OpenAI from 'openai';
import type { ResponseInputItem, Tool } from 'openai/resources/responses/responses';
import { withFiles } from './commands/index.ts';
import { commandTools, runCommandTool } from './commands/tools.ts';
import type { Env } from './env.ts';
import { takeUsage } from './usage.ts';

const HISTORY_LIMIT = 50;
const MAX_MESSAGE_LENGTH = 2_000;
const MAX_TOOL_ROUNDS = 3;
const COMMAND_TIMEOUT_MS = 120_000;
const REPLY_TIMEOUT_MS = 180_000;
// Discord shows "typing…" for about 10 seconds.
const TYPING_INTERVAL_MS = 8_000;
const FAILURE_REPLY = "Sorry, I couldn't come up with a reply. Try again in a moment.";
const TOOLS: Tool[] = [
  ...commandTools,
  { type: 'web_search' },
  { type: 'code_interpreter', container: { type: 'auto' } },
];

type Message = Pick<APIMessage, 'author' | 'content' | 'mentions'>;

export function addressesBot(message: GatewayMessageCreateDispatchData, token: string) {
  const botId = botUserId(token);
  const isDM = !message.guild_id;
  return !message.author.bot && (isDM || message.mentions.some((user) => user.id === botId));
}

export async function replyToMention(
  message: GatewayMessageCreateDispatchData,
  token: string,
  env: Env,
  sql: SqlStorage,
  signal = AbortSignal.timeout(REPLY_TIMEOUT_MS),
) {
  const dm = !message.guild_id;
  const messages = Routes.channelMessages(message.channel_id);
  const discord = (route: string, init?: RequestInit) =>
    fetch(`${RouteBases.api}${route}`, {
      ...init,
      headers: { Authorization: `Bot ${token}` },
      redirect: 'manual',
    });
  const typing = () =>
    discord(Routes.channelTyping(message.channel_id), { method: 'POST' }).catch(() => undefined);
  const send = async (content: string, files: File[] = []) => {
    const body = {
      content,
      ...(!dm && { message_reference: { message_id: message.id, fail_if_not_exists: false } }),
      allowed_mentions: { parse: [], replied_user: true },
      flags: MessageFlags.SuppressEmbeds,
    } satisfies RESTPostAPIChannelMessageJSONBody;
    const sent = await discord(messages, { method: 'POST', body: withFiles(body, files) });
    if (!sent.ok) console.error('Discord chat reply failed', sent.status);
  };

  const user = { id: message.author.id, sql };
  const use = takeUsage(sql, 'text', user.id);
  if ('refused' in use) return send(use.refused);

  let content = FAILURE_REPLY;
  let files: File[] = [];
  try {
    const [, response, me] = await Promise.all([
      typing(),
      discord(`${messages}?before=${message.id}&limit=${HISTORY_LIMIT - 1}`, { signal }),
      discord(Routes.user(), { signal }),
    ]);
    if (!me.ok) throw new Error(`Discord returned ${me.status} for the bot user`);
    const bot = await me.json<APIUser>();
    const history = response.ok ? (await response.json<APIMessage[]>()).reverse() : [];
    const image = [message, message.referenced_message, ...history.toReversed()]
      .flatMap((source) => source?.attachments ?? [])
      .find((attachment) => attachment.content_type?.startsWith('image/'));
    ({ content, files } = await generate(
      [...history, message],
      bot,
      dm,
      env,
      typing,
      user,
      image,
      signal,
    ));
  } catch (error) {
    use.refund();
    console.error('Chat reply failed', error instanceof Error ? error.message : error);
  }
  await send(content, files);
}

async function generate(
  messages: Message[],
  bot: APIUser,
  dm: boolean,
  env: Env,
  typing: () => Promise<unknown>,
  user: { id: string; sql: SqlStorage },
  image: APIAttachment | undefined,
  signal: AbortSignal,
) {
  if (!env.OPENAI_API_KEY) throw new Error('OPENAI_API_KEY is not set');
  const [place, like, commands] = dm
    ? ['a Discord DM', 'a friend texting', 'your own']
    : ['a Discord channel', 'a regular member of the server', "this server's"];
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

  for (let round = 0; round <= MAX_TOOL_ROUNDS; round++) {
    const response = await client.responses.create(
      {
        model: 'gpt-6.1-terra',
        instructions: `You are ${displayName(bot)}, a bot chatting in ${place}. Be casual, friendly and concise, like ${like}: usually a sentence or two, and always under 1,500 characters. Each message from someone else starts with their name. Reply to the last message, using earlier messages as context. Write only your reply, without your name in front. Search the web when the last message needs current or recent information, and use the python tool for math or data you'd otherwise work out in your head. You can't share files or charts, but images your commands make are attached to your reply automatically. Your other tools are ${commands} slash commands. Use one when someone asks for it or it clearly fits the last message, instead of doing its job yourself (for example, don't make a random pick yourself when a command can). Only use commands for the last message, and report their results exactly.`,
        input,
        tools: TOOLS,
        tool_choice: round < MAX_TOOL_ROUNDS ? 'auto' : 'none',
        reasoning: { effort: 'none' },
        max_output_tokens: 500,
      },
      { signal },
    );
    const calls = response.output.filter((item) => item.type === 'function_call');
    if (!calls.length) {
      const text = response.output_text.trim();
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
        used.add(call.name);
        const deadline = AbortSignal.any([signal, AbortSignal.timeout(COMMAND_TIMEOUT_MS)]);
        const result = await runCommandTool(call.name, call.arguments, env, deadline, user, image);
        files.push(...result.files);
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

// A bot token starts with the bot's user ID in base64.
function botUserId(token: string) {
  return atob(token.split('.')[0]!);
}
