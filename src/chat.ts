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
import { commandTools, runCommandTool } from './commands/tools.ts';
import type { Env } from './env.ts';

const HISTORY_LIMIT = 50;
const MAX_MESSAGE_LENGTH = 2_000;
const MAX_TOOL_ROUNDS = 3;
const COMMAND_TIMEOUT_MS = 15_000;
const REPLY_TIMEOUT_MS = 60_000;
const FAILURE_REPLY = "Sorry, I couldn't come up with a reply. Try again in a moment.";
const TOOLS: Tool[] = [
  ...commandTools,
  { type: 'web_search' },
  { type: 'code_interpreter', container: { type: 'auto' } },
];

type Message = Pick<APIMessage, 'author' | 'content' | 'mentions'>;

export function mentionsBot(message: GatewayMessageCreateDispatchData, token: string) {
  const botId = botUserId(token);
  return !message.author.bot && message.mentions.some((user) => user.id === botId);
}

export async function replyToMention(
  message: GatewayMessageCreateDispatchData,
  token: string,
  env: Env,
  signal = AbortSignal.timeout(REPLY_TIMEOUT_MS),
) {
  const messages = Routes.channelMessages(message.channel_id);
  const discord = (route: string, init?: RequestInit) =>
    fetch(`${RouteBases.api}${route}`, {
      ...init,
      headers: { Authorization: `Bot ${token}`, 'Content-Type': 'application/json' },
      redirect: 'manual',
    });
  const typing = () =>
    discord(Routes.channelTyping(message.channel_id), { method: 'POST' }).catch(() => undefined);

  let content = FAILURE_REPLY;
  try {
    const [, response] = await Promise.all([
      typing(),
      discord(`${messages}?before=${message.id}&limit=${HISTORY_LIMIT - 1}`, { signal }),
    ]);
    const history = response.ok ? (await response.json<APIMessage[]>()).reverse() : [];
    const image = [message, message.referenced_message, ...history.toReversed()]
      .flatMap((source) => source?.attachments ?? [])
      .find((attachment) => attachment.content_type?.startsWith('image/'));
    content = await generate([...history, message], botUserId(token), env, typing, image, signal);
  } catch (error) {
    console.error('Chat reply failed', error instanceof Error ? error.message : error);
  }

  const body = {
    content,
    message_reference: { message_id: message.id, fail_if_not_exists: false },
    allowed_mentions: { parse: [], replied_user: true },
    flags: MessageFlags.SuppressEmbeds,
  } satisfies RESTPostAPIChannelMessageJSONBody;
  const sent = await discord(messages, { method: 'POST', body: JSON.stringify(body) });
  if (!sent.ok) console.error('Discord chat reply failed', sent.status);
}

async function generate(
  messages: Message[],
  botId: string,
  env: Env,
  typing: () => Promise<unknown>,
  image: APIAttachment | undefined,
  signal: AbortSignal,
) {
  if (!env.OPENAI_API_KEY) throw new Error('OPENAI_API_KEY is not set');
  const bot = messages.at(-1)!.mentions.find((user) => user.id === botId)!;
  const client = new OpenAI({ apiKey: env.OPENAI_API_KEY, timeout: 30_000 });
  const input = messages.flatMap((message): ResponseInputItem[] => {
    const text = readable(message);
    if (!text) return [];
    return message.author.id === botId
      ? [{ role: 'assistant', content: text }]
      : [{ role: 'user', content: `${displayName(message.author)}: ${text}` }];
  });
  const used = new Set<string>();

  for (let round = 0; round <= MAX_TOOL_ROUNDS; round++) {
    const response = await client.responses.create(
      {
        model: 'gpt-6.1-terra',
        instructions: `You are ${displayName(bot)}, a bot chatting in a Discord channel. Be casual, friendly and concise, like a regular member of the server: usually a sentence or two, and always under 1,500 characters. Each message from someone else starts with their name. Reply to the last message, using earlier messages as context. Write only your reply, without your name in front. Search the web when the last message needs current or recent information, and use the python tool for math or data you'd otherwise work out in your head. You can't share files or charts, so answer in text. Your other tools are this server's slash commands. Use one when someone asks for it or it clearly fits the last message, instead of doing its job yourself (for example, don't make a random pick yourself when a command can). Only use commands for the last message, and report their results exactly.`,
        input,
        tools: TOOLS,
        tool_choice: round < MAX_TOOL_ROUNDS ? 'auto' : 'none',
        reasoning: { effort: 'none' },
        max_output_tokens: 500,
      },
      { signal },
    );
    const calls = response.output.filter((item) => item.type === 'function_call');
    if (!calls.length) return withNote(response.output_text.trim(), used);

    void typing();
    const outputs = await Promise.all(
      calls.map(async (call): Promise<ResponseInputItem> => {
        used.add(call.name);
        const deadline = AbortSignal.any([signal, AbortSignal.timeout(COMMAND_TIMEOUT_MS)]);
        const output = await runCommandTool(call.name, call.arguments, env, deadline, image);
        return { type: 'function_call_output', call_id: call.call_id, output };
      }),
    );
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
  if (!text) throw new Error('OpenAI returned no text');
  const note = used.size ? `\n-# used ${[...used].map((name) => `/${name}`).join(', ')}` : '';
  const limit = MAX_MESSAGE_LENGTH - note.length;
  return (text.length > limit ? `${text.slice(0, limit - 1)}…` : text) + note;
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
