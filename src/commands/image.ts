import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteractionResponseChannelMessageWithSource,
} from 'discord-api-types/v10';
import OpenAI from 'openai';
import type { ImagesResponse } from 'openai/resources/images';
import { download } from './caption.ts';
import type { CommandResponse, SlashCommand } from './command.ts';

const OUTPUT = {
  model: 'gpt-image-2.5-flare',
  size: 'auto',
  quality: 'medium',
  output_format: 'png',
} as const;
const EDITABLE_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);
const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
const MAX_PROMPT_LENGTH = 1_000;

export const imagine = {
  definition: {
    name: 'imagine',
    description: 'Generate an image from a description.',
    type: ApplicationCommandType.ChatInput,
    options: [
      {
        type: ApplicationCommandOptionType.String,
        name: 'prompt',
        description: 'What the image should show',
        required: true,
        min_length: 1,
        max_length: MAX_PROMPT_LENGTH,
      },
    ],
  },
  deferred: true,
  limit: 'image',

  async execute(data, env, signal): Promise<CommandResponse> {
    const prompt = promptOption(data);
    if (!prompt) return ephemeral('Describe the image you want.');
    if (!env.OPENAI_API_KEY) return ephemeral('Images are not configured yet.');

    const client = new OpenAI({ apiKey: env.OPENAI_API_KEY });
    return respond(prompt, () => client.images.generate({ ...OUTPUT, prompt }, { signal }));
  },
} satisfies SlashCommand;

export const edit = {
  definition: {
    name: 'edit',
    description: 'Change an image based on a description.',
    type: ApplicationCommandType.ChatInput,
    options: [
      {
        type: ApplicationCommandOptionType.Attachment,
        name: 'image',
        description: 'The image to change',
        required: true,
      },
      {
        type: ApplicationCommandOptionType.String,
        name: 'prompt',
        description: 'How to change the image',
        required: true,
        min_length: 1,
        max_length: MAX_PROMPT_LENGTH,
      },
    ],
  },
  deferred: true,
  limit: 'image',

  async execute(data, env, signal): Promise<CommandResponse> {
    const option = data.options?.find((option) => option.name === 'image');
    const image =
      option?.type === ApplicationCommandOptionType.Attachment
        ? data.resolved?.attachments?.[option.value]
        : undefined;
    const prompt = promptOption(data);
    if (!image?.content_type || !EDITABLE_TYPES.has(image.content_type)) {
      return ephemeral('Upload a PNG, JPEG, or WebP image to edit.');
    }
    if (image.size > MAX_IMAGE_BYTES) {
      return ephemeral('That image is too big. Upload one under 10 MB.');
    }
    if (!prompt) return ephemeral('Describe how to change the image.');
    if (!env.OPENAI_API_KEY) return ephemeral('Images are not configured yet.');

    const client = new OpenAI({ apiKey: env.OPENAI_API_KEY });
    const { url, filename, content_type: type } = image;
    return respond(prompt, async () => {
      const file = await download(url, filename, type, signal);
      return client.images.edit({ ...OUTPUT, image: file, prompt }, { signal });
    });
  },
} satisfies SlashCommand;

function promptOption(data: APIChatInputApplicationCommandInteractionData) {
  const option = data.options?.find((option) => option.name === 'prompt');
  return option?.type === ApplicationCommandOptionType.String ? option.value.trim() : '';
}

async function respond(
  prompt: string,
  create: () => Promise<ImagesResponse>,
): Promise<CommandResponse> {
  try {
    const base64 = (await create()).data?.[0]?.b64_json;
    if (!base64) throw new Error('OpenAI returned no image');
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
    return {
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: `> ${prompt.replace(/\s+/g, ' ')}`, allowed_mentions: { parse: [] } },
      files: [new File([bytes], 'image.png', { type: 'image/png' })],
    };
  } catch (error) {
    if (error instanceof OpenAI.APIError && error.code === 'moderation_blocked') {
      return ephemeral("OpenAI's safety filter blocked that. Try a different description.");
    }
    console.error('Image command failed', error instanceof Error ? error.message : error);
    return ephemeral("I couldn't make that image. Try again.");
  }
}

function ephemeral(content: string): APIInteractionResponseChannelMessageWithSource {
  return {
    type: InteractionResponseType.ChannelMessageWithSource,
    data: { content, flags: MessageFlags.Ephemeral },
  };
}
