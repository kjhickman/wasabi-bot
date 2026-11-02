import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { caption } from '../src/commands/caption.ts';
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
  it('offers /conch, /choose and /caption as strict tools', () => {
    expect(commandTools).toEqual([
      {
        type: 'function',
        name: 'conch',
        description: 'Ask the magic conch a yes/no question.',
        strict: true,
        parameters: {
          type: 'object',
          properties: { question: { type: 'string', description: expect.any(String) } },
          required: ['question'],
          additionalProperties: false,
        },
      },
      {
        type: 'function',
        name: 'choose',
        description: 'Randomly pick one of 2 to 7 options.',
        strict: true,
        parameters: {
          type: 'object',
          properties: Object.fromEntries(
            [1, 2, 3, 4, 5, 6, 7].map((n) => [
              `option${n}`,
              { type: n <= 2 ? 'string' : ['string', 'null'], description: `Option ${n}` },
            ]),
          ),
          required: Object.keys(chooseArgs),
          additionalProperties: false,
        },
      },
      {
        type: 'function',
        name: 'caption',
        description:
          'Get a funny meme caption for an image. It uses the image attached to the last message, else the one on the message it replies to, else the most recent image in the channel.',
        strict: true,
        parameters: { type: 'object', properties: {}, required: [], additionalProperties: false },
      },
    ]);
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

    expect(await runCommandTool('choose', JSON.stringify(chooseArgs), env, signal)).toBe(
      'Choosing between: WoW, Overwatch\nI choose **Overwatch**!',
    );
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
    expect(await runCommandTool(name, args, env, signal)).toBe(expected);
  });

  it('fills attachment options with the conversation’s image', async () => {
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
    });

    expect(await runCommandTool('caption', '{}', env, signal, image)).toBe(
      'Me when the build passes',
    );
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
