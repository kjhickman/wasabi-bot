import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { caption } from '../src/commands/caption.ts';
import { imagine } from '../src/commands/image.ts';
import { commands } from '../src/commands/index.ts';
import { commandTool, commandTools, runCommandTool } from '../src/commands/tools.ts';
import type { Env } from '../src/env.ts';

const env = {} as Env;
const signal = new AbortController().signal;
const chooseArgs = {
  option1: 'WoW',
  option2: 'Overwatch',
  option3: null,
  option4: null,
  option5: null,
  option6: null,
  option7: null,
};

function definition(extra: object) {
  return {
    name: 'test',
    description: 'A test command.',
    type: ApplicationCommandType.ChatInput,
    ...extra,
  } as RESTPostAPIChatInputApplicationCommandsJSONBody;
}

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('commandTools', () => {
  it('offers every command as a tool', () => {
    expect(commandTools.map((tool) => tool.name)).toEqual(
      commands.map((command) => command.definition.name),
    );
  });

  it('leaves image options out of the schema and describes where the image comes from', () => {
    expect(commandTools.find((tool) => tool.name === 'caption')).toEqual({
      type: 'function',
      name: 'caption',
      description:
        'Get a funny meme caption for an image. It uses the image attached to the last message, else the one on the message it replies to, else the most recent image in the channel.',
      strict: true,
      parameters: { type: 'object', properties: {}, required: [], additionalProperties: false },
    });
  });

  it('asks the model only for the prompt when editing an image', () => {
    expect(commandTools.find((tool) => tool.name === 'edit')).toEqual({
      type: 'function',
      name: 'edit',
      description:
        'Change an image based on a description. It uses the image attached to the last message, else the one on the message it replies to, else the most recent image in the channel.',
      strict: true,
      parameters: {
        type: 'object',
        properties: { prompt: { type: 'string', description: 'How to change the image' } },
        required: ['prompt'],
        additionalProperties: false,
      },
    });
  });

  it('maps choices and number limits, and allows null for optional options', () => {
    const tool = commandTool(
      definition({
        options: [
          {
            type: ApplicationCommandOptionType.Integer,
            name: 'sides',
            description: 'Sides',
            required: true,
            min_value: 2,
            max_value: 100,
          },
          {
            type: ApplicationCommandOptionType.String,
            name: 'color',
            description: 'Color',
            choices: [
              { name: 'Red', value: 'red' },
              { name: 'Blue', value: 'blue' },
            ],
          },
          { type: ApplicationCommandOptionType.Boolean, name: 'secret', description: 'Secret' },
        ],
      }),
    );

    expect(tool!.parameters).toEqual({
      type: 'object',
      properties: {
        sides: { type: 'integer', description: 'Sides', minimum: 2, maximum: 100 },
        color: { type: ['string', 'null'], description: 'Color', enum: ['red', 'blue', null] },
        secret: { type: ['boolean', 'null'], description: 'Secret' },
      },
      required: ['sides', 'color', 'secret'],
      additionalProperties: false,
    });
  });

  it.each([
    ['is limited to some members', { default_member_permissions: '0' }],
    [
      'takes a user',
      { options: [{ type: ApplicationCommandOptionType.User, name: 'who', description: 'Who' }] },
    ],
    [
      'has subcommands',
      {
        options: [
          { type: ApplicationCommandOptionType.Subcommand, name: 'add', description: 'Add' },
        ],
      },
    ],
  ])('leaves out a command that %s', (_, extra) => {
    expect(commandTool(definition(extra))).toBeUndefined();
  });
});

describe('runCommandTool', () => {
  it('runs the command with the arguments that are not null', async () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.9);

    expect(await runCommandTool('choose', JSON.stringify(chooseArgs), env, signal)).toEqual({
      output: 'Choosing between: WoW, Overwatch\nI choose **Overwatch**!',
      files: [],
    });
  });

  it('passes on the files a command makes and tells the model they are attached', async () => {
    const file = new File(['png'], 'image.png', { type: 'image/png' });
    vi.spyOn(imagine, 'execute').mockResolvedValue({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: '> a frog' },
      files: [file],
    });

    expect(
      await runCommandTool('imagine', JSON.stringify({ prompt: 'a frog' }), env, signal),
    ).toEqual({ output: '> a frog\nThe image is attached to your reply.', files: [file] });
  });

  it.each([
    ['an unknown command', 'nope', '{}', 'Error: unknown command.'],
    [
      'a private error reply',
      'choose',
      JSON.stringify({ ...chooseArgs, option2: ' ' }),
      'Error: Give me at least 2 options to choose from.',
    ],
    [
      'text longer than Discord allows',
      'choose',
      JSON.stringify({ ...chooseArgs, option1: 'a'.repeat(101) }),
      'Error: option1 must be 1 to 100 characters long.',
    ],
    ['invalid JSON', 'choose', '{', 'Error: the command failed.'],
    [
      'an image command without an image',
      'caption',
      '{}',
      'Error: Upload a PNG, JPEG, WebP, or GIF image to caption.',
    ],
  ])('returns an error for %s', async (_, name, args, expected) => {
    expect(await runCommandTool(name, args, env, signal)).toEqual({ output: expected, files: [] });
  });

  it('fills attachment options with the conversation’s image, and keeps captions text-only', async () => {
    const image = {
      id: '700',
      filename: 'cat.png',
      content_type: 'image/png',
      size: 4,
      url: 'https://cdn.discordapp.com/attachments/42/700/cat.png',
      proxy_url: 'https://media.discordapp.net/attachments/42/700/cat.png',
    };
    const execute = vi.spyOn(caption, 'execute').mockResolvedValue({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Me when the build passes' },
      files: [new File(['png'], 'cat.png', { type: 'image/png' })],
    });

    expect(await runCommandTool('caption', '{}', env, signal, image)).toEqual({
      output: 'Me when the build passes',
      files: [],
    });
    expect(execute).toHaveBeenCalledExactlyOnceWith(
      {
        id: '0',
        type: ApplicationCommandType.ChatInput,
        name: 'caption',
        options: [{ name: 'image', type: ApplicationCommandOptionType.Attachment, value: '700' }],
        resolved: { attachments: { '700': image } },
      },
      env,
      signal,
    );
  });
});
