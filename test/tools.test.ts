import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { caption } from '../src/commands/caption.ts';
import { CommandError } from '../src/commands/command.ts';
import { imagine } from '../src/commands/image.ts';
import { commands } from '../src/commands/index.ts';
import { radio } from '../src/commands/radio.ts';
import {
  chatTools,
  commandLabel,
  commandTools,
  offers,
  runCommandTool,
  runTool,
  type ToolContext,
} from '../src/chat/tools.ts';
import type { Env } from '../src/env.ts';
import { takeUsage } from '../src/usage.ts';
import { chooseArgs } from './fixtures/builders.ts';
import { sqlStorage } from './fixtures/sql.ts';

const env = {} as Env;
const signal = new AbortController().signal;
let context: ToolContext;
function definition(extra: object) {
  return {
    name: 'test',
    description: 'A test command.',
    type: ApplicationCommandType.ChatInput,
    ...extra,
  } as RESTPostAPIChatInputApplicationCommandsJSONBody;
}

beforeEach(() => {
  context = { env, sql: sqlStorage(), userId: '200', signal };
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

function imageUses() {
  return context.sql.exec("SELECT * FROM usage WHERE kind = 'image'").toArray().length;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('commandTools', () => {
  it('offers every command as a tool, and each /radio subcommand as its own', () => {
    expect(commandTools.map((tool) => tool.name)).toEqual(
      commands.flatMap(({ definition: { name } }) =>
        name === 'radio' ? ['radio_play', 'radio_stop'] : [name],
      ),
    );
  });

  it('builds a subcommand’s tool from its own description and options', () => {
    expect(
      offers(
        definition({
          options: [
            {
              type: ApplicationCommandOptionType.Subcommand,
              name: 'add',
              description: 'Add a thing.',
              options: [
                {
                  type: ApplicationCommandOptionType.String,
                  name: 'what',
                  description: 'What to add',
                  required: true,
                },
              ],
            },
            {
              type: ApplicationCommandOptionType.Subcommand,
              name: 'clear',
              description: 'Clear it.',
            },
          ],
        }),
      ).map((entry) => entry.tool),
    ).toEqual([
      expect.objectContaining({
        name: 'test_add',
        description: 'Add a thing.',
        parameters: expect.objectContaining({
          properties: { what: { type: 'string', description: 'What to add' } },
        }),
      }),
      expect.objectContaining({ name: 'test_clear', description: 'Clear it.' }),
    ]);
  });

  it('labels tools as the slash command someone would type', () => {
    expect(commandLabel('radio_play')).toBe('radio play');
    expect(commandLabel('choose')).toBe('choose');
    expect(commandLabel('radio_search')).toBeUndefined();
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
    const [entry] = offers(
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

    expect(entry!.tool.parameters).toEqual({
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
      'has subcommand groups',
      {
        options: [
          { type: ApplicationCommandOptionType.SubcommandGroup, name: 'tags', description: 'Tags' },
        ],
      },
    ],
  ])('leaves out a command that %s', (_, extra) => {
    expect(offers(definition(extra))).toEqual([]);
  });
});

describe('runCommandTool', () => {
  it('runs the command with the arguments that are not null', async () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.9);

    expect(await runCommandTool('choose', JSON.stringify(chooseArgs), context)).toEqual({
      output: 'Choosing between: WoW, Overwatch\nI choose **Overwatch**!',
      files: [],
    });
  });

  it('runs a subcommand for the server the conversation is in', async () => {
    const execute = vi.spyOn(radio, 'execute').mockResolvedValue({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: 'Playing **Classic Rock 109** in <#400>.' },
    });

    const result = await runCommandTool('radio_play', JSON.stringify({ station: 'classic rock' }), {
      ...context,
      guildId: '300',
    });

    expect(result).toEqual({ output: 'Playing **Classic Rock 109** in <#400>.', files: [] });
    expect(execute).toHaveBeenCalledExactlyOnceWith(
      {
        id: '0',
        type: ApplicationCommandType.ChatInput,
        name: 'radio',
        options: [
          {
            type: ApplicationCommandOptionType.Subcommand,
            name: 'play',
            options: [
              { name: 'station', type: ApplicationCommandOptionType.String, value: 'classic rock' },
            ],
          },
        ],
      },
      env,
      signal,
      { guildId: '300', userId: '200' },
    );
  });

  it('passes on the files a command makes and tells the model they are attached', async () => {
    const file = new File(['png'], 'image.png', { type: 'image/png' });
    vi.spyOn(imagine, 'execute').mockResolvedValue({
      type: InteractionResponseType.ChannelMessageWithSource,
      data: { content: '> a frog' },
      files: [file],
    });

    expect(await runCommandTool('imagine', JSON.stringify({ prompt: 'a frog' }), context)).toEqual({
      output: '> a frog\nThe image is attached to your reply.',
      files: [file],
    });
    expect(imageUses()).toBe(1);
  });

  it('does not count an image the command failed to make', async () => {
    vi.spyOn(imagine, 'execute').mockRejectedValue(
      new CommandError("I couldn't make that image. Try again."),
    );

    await runCommandTool('imagine', JSON.stringify({ prompt: 'a frog' }), context);

    expect(imageUses()).toBe(0);
  });

  it('refuses an image over the daily limit without running the command', async () => {
    for (let i = 0; i < 50; i++) takeUsage(context.sql, 'image', context.userId);
    const execute = vi.spyOn(imagine, 'execute');

    const { output } = await runCommandTool(
      'imagine',
      JSON.stringify({ prompt: 'a frog' }),
      context,
    );

    expect(output).toMatch(/^Error: You've used all 50 of your images for today\. Try again in /);
    expect(execute).not.toHaveBeenCalled();
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
    expect(await runCommandTool(name, args, context)).toEqual({
      output: expected,
      files: [],
    });
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

    expect(await runCommandTool('caption', '{}', { ...context, image })).toEqual({
      output: 'Me when the build passes',
      files: [],
    });
    expect(context.sql.exec('SELECT * FROM usage').toArray()).toEqual([]);
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
      { guildId: undefined, userId: '200' },
    );
  });
});

describe('runTool', () => {
  const ROCK = {
    stationuuid: 'rock-1',
    name: 'Classic Rock 109',
    codec: 'MP3',
    bitrate: 128,
    countrycode: 'US',
    tags: 'classic rock,rock',
    votes: 5_000,
    clickcount: 80,
  };
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset().mockImplementation(async () => Response.json([ROCK]));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('offers the model a radio search alongside the commands', () => {
    expect(chatTools.map((tool) => tool.name)).toEqual([
      ...commandTools.map((tool) => tool.name),
      'radio_search',
    ]);
  });

  it('searches radio stations for the model', async () => {
    const { output, files } = await runTool(
      'radio_search',
      JSON.stringify({ query: ' classic rock ' }),
      context,
    );

    expect(JSON.parse(output)).toEqual([
      {
        id: 'rock-1',
        name: 'Classic Rock 109',
        country: 'US',
        quality: 'MP3 128 kbps',
        tags: ['classic rock', 'rock'],
        votes: 5_000,
      },
    ]);
    expect(files).toEqual([]);
    expect(new URL(String(fetchMock.mock.calls[0]![0])).searchParams.get('name')).toBe(
      'classic rock',
    );
  });

  it('says when no station matches', async () => {
    fetchMock.mockImplementation(async () => Response.json([]));

    expect(
      await runTool('radio_search', JSON.stringify({ query: 'polka metal' }), context),
    ).toEqual({ output: 'No stations matched "polka metal".', files: [] });
  });

  it('reports a search that fails', async () => {
    fetchMock.mockImplementation(async () => new Response('Down', { status: 502 }));

    expect(await runTool('radio_search', JSON.stringify({ query: 'jazz' }), context)).toEqual({
      output: 'Error: the station search failed.',
      files: [],
    });
    expect(console.error).toHaveBeenCalledWith(
      expect.objectContaining({
        message: 'Chat radio search failed',
        error: expect.objectContaining({ message: 'Radio Browser returned HTTP 502' }),
      }),
    );
  });

  it('runs anything else as a command', async () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.9);

    expect(await runTool('choose', JSON.stringify(chooseArgs), context)).toEqual({
      output: 'Choosing between: WoW, Overwatch\nI choose **Overwatch**!',
      files: [],
    });
  });
});
