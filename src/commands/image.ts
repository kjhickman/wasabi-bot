import { ApplicationCommandOptionType, ApplicationCommandType } from 'discord-api-types/v10';
import OpenAI from 'openai';
import type { ImagesResponse } from 'openai/resources/images';
import { attachmentOption, download, MAX_UPLOAD_BYTES, stringOption } from '../discord.ts';
import { log, preview } from '../log.ts';
import { MODELS } from '../openai.ts';
import { CommandError, reply, type SlashCommand } from './command.ts';

const IMAGE_OPTIONS = {
  model: MODELS.image,
  size: 'auto',
  quality: 'medium',
  output_format: 'png',
} as const;
const EDITABLE_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);
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

  async execute(data, env, signal) {
    const prompt = stringOption(data, 'prompt');
    if (!prompt) throw new CommandError('Describe the image you want.');
    if (!env.OPENAI_API_KEY) throw new CommandError('Images are not configured yet.');

    const client = new OpenAI({ apiKey: env.OPENAI_API_KEY });
    return respond(prompt, () => client.images.generate({ ...IMAGE_OPTIONS, prompt }, { signal }));
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

  async execute(data, env, signal) {
    const image = attachmentOption(data, 'image');
    const prompt = stringOption(data, 'prompt');
    if (!image?.content_type || !EDITABLE_TYPES.has(image.content_type)) {
      throw new CommandError('Upload a PNG, JPEG, or WebP image to edit.');
    }
    if (image.size > MAX_UPLOAD_BYTES) {
      throw new CommandError('That image is too big. Upload one under 10 MB.');
    }
    if (!prompt) throw new CommandError('Describe how to change the image.');
    if (!env.OPENAI_API_KEY) throw new CommandError('Images are not configured yet.');

    const client = new OpenAI({ apiKey: env.OPENAI_API_KEY });
    return respond(prompt, async () => {
      const file = await download(image, signal);
      return client.images.edit({ ...IMAGE_OPTIONS, image: file, prompt }, { signal });
    });
  },
} satisfies SlashCommand;

async function respond(prompt: string, create: () => Promise<ImagesResponse>) {
  try {
    const base64 = (await create()).data?.[0]?.b64_json;
    if (!base64) throw new Error('OpenAI returned no image');
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
    return reply({ content: `> ${prompt.replace(/\s+/g, ' ')}`, allowed_mentions: { parse: [] } }, [
      new File([bytes], 'image.png', { type: 'image/png' }),
    ]);
  } catch (error) {
    if (error instanceof OpenAI.APIError && error.code === 'moderation_blocked') {
      throw new CommandError("OpenAI's safety filter blocked that. Try a different description.");
    }
    log.error('Image command failed', { error, input: preview(prompt) });
    throw new CommandError("I couldn't make that image. Try again.");
  }
}
