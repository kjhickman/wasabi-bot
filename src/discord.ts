import {
  ApplicationCommandOptionType,
  RouteBases,
  type APIAttachment,
  type APIChatInputApplicationCommandInteractionData,
} from 'discord-api-types/v10';

// Nitro members can send 4,000 characters, but bots are limited to 2,000.
export const MAX_MESSAGE_LENGTH = 2_000;
// Discord's default upload limit for bots.
export const MAX_UPLOAD_BYTES = 10 * 1024 * 1024;
export const DISCORD_EPOCH_MS = 1_420_070_400_000;

export function discordFetch(
  route: string,
  { token, ...init }: RequestInit & { token?: string } = {},
) {
  const headers = new Headers(init.headers);
  if (token) headers.set('Authorization', `Bot ${token}`);
  return fetch(`${RouteBases.api}${route}`, { ...init, headers, redirect: 'manual' });
}

export function withFiles(message: object, files: File[]) {
  const body = new FormData();
  body.set(
    'payload_json',
    JSON.stringify({
      ...message,
      attachments: files.map((file, id) => ({ id, filename: file.name })),
    }),
  );
  files.forEach((file, i) => body.set(`files[${i}]`, file));
  return body;
}

export async function download(
  { url, filename, content_type }: APIAttachment,
  signal: AbortSignal,
) {
  const response = await fetch(url, { signal });
  if (!response.ok) throw new Error(`Attachment download failed: ${response.status}`);
  return new File([await response.arrayBuffer()], filename, { type: content_type ?? '' });
}

// When a snowflake ID was created, in milliseconds since the Unix epoch.
export function snowflakeTime(id: string) {
  return Number(BigInt(id) >> 22n) + DISCORD_EPOCH_MS;
}

export function stringOption(data: APIChatInputApplicationCommandInteractionData, name: string) {
  const option = data.options?.find((option) => option.name === name);
  return option?.type === ApplicationCommandOptionType.String ? option.value.trim() : '';
}

export function attachmentOption(
  data: APIChatInputApplicationCommandInteractionData,
  name: string,
) {
  const option = data.options?.find((option) => option.name === name);
  return option?.type === ApplicationCommandOptionType.Attachment
    ? data.resolved?.attachments?.[option.value]
    : undefined;
}
