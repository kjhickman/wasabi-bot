import type {
  APIMessage,
  APIUser,
  GatewayMessageCreateDispatchData,
  RESTPostAPIChannelMessageJSONBody,
} from 'discord-api-types/v10';
import OpenAI from 'openai';
import type { EasyInputMessage } from 'openai/resources/responses/responses';

const HISTORY_LIMIT = 50;
const MAX_MESSAGE_LENGTH = 2_000;
const FAILURE_REPLY = "Sorry, I couldn't come up with a reply. Try again in a moment.";

type Message = Pick<APIMessage, 'author' | 'content' | 'mentions'>;

export function mentionsBot(message: GatewayMessageCreateDispatchData, token: string) {
  const botId = botUserId(token);
  return !message.author.bot && message.mentions.some((user) => user.id === botId);
}

export async function replyToMention(
  message: GatewayMessageCreateDispatchData,
  token: string,
  apiKey: string | undefined,
) {
  const discord = (path: string, init?: RequestInit) =>
    fetch(`https://discord.com/api/v10/channels/${message.channel_id}${path}`, {
      ...init,
      headers: { Authorization: `Bot ${token}`, 'Content-Type': 'application/json' },
      redirect: 'manual',
    });

  let content = FAILURE_REPLY;
  try {
    const [, response] = await Promise.all([
      discord('/typing', { method: 'POST' }).catch(() => undefined),
      discord(`/messages?before=${message.id}&limit=${HISTORY_LIMIT - 1}`),
    ]);
    const history = response.ok ? (await response.json<APIMessage[]>()).reverse() : [];
    content = await generate([...history, message], botUserId(token), apiKey);
  } catch (error) {
    console.error('Chat reply failed', error instanceof Error ? error.message : error);
  }

  const body = {
    content,
    message_reference: { message_id: message.id, fail_if_not_exists: false },
    allowed_mentions: { parse: [], replied_user: true },
  } satisfies RESTPostAPIChannelMessageJSONBody;
  const sent = await discord('/messages', { method: 'POST', body: JSON.stringify(body) });
  if (!sent.ok) console.error('Discord chat reply failed', sent.status);
}

async function generate(messages: Message[], botId: string, apiKey: string | undefined) {
  if (!apiKey) throw new Error('OPENAI_API_KEY is not set');
  const bot = messages.at(-1)!.mentions.find((user) => user.id === botId)!;
  const client = new OpenAI({ apiKey, timeout: 30_000 });
  const response = await client.responses.create({
    model: 'gpt-6.1-terra',
    instructions: `You are ${displayName(bot)}, a bot chatting in a Discord channel. Be casual, friendly and concise, like a regular member of the server: usually a sentence or two, and always under 1,500 characters. Each message from someone else starts with their name. Reply to the last message, using earlier messages as context. Write only your reply, without your name in front.`,
    input: messages.flatMap((message): EasyInputMessage[] => {
      const text = readable(message);
      if (!text) return [];
      return message.author.id === botId
        ? [{ role: 'assistant', content: text }]
        : [{ role: 'user', content: `${displayName(message.author)}: ${text}` }];
    }),
    reasoning: { effort: 'none' },
    max_output_tokens: 500,
  });
  const text = response.output_text.trim();
  if (!text) throw new Error('OpenAI returned no text');
  return text.length > MAX_MESSAGE_LENGTH ? `${text.slice(0, MAX_MESSAGE_LENGTH - 1)}…` : text;
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
