import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionResponseType,
  InteractionType,
  MessageFlags,
  type APIChatInputApplicationCommandInteractionData,
  type APIInteraction,
} from 'discord-api-types/v10';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vite-plus/test';
import { CommandError } from '../src/commands/command.ts';
import { radio } from '../src/commands/radio.ts';
import type { Env } from '../src/env.ts';
import { handleInteraction } from '../src/gateway/interactions.ts';
import { findStation, searchStations, stationChoices } from '../src/radio/stations.ts';
import { snowflake } from './fixtures/builders.ts';
import { sqlStorage } from './fixtures/sql.ts';

const JAZZ = {
  stationuuid: 'd28420a4-eccf-47a2-ace1-088c7e7cb7e0',
  name: '  101 SMOOTH JAZZ ',
  codec: 'MP3',
  bitrate: 192,
  countrycode: 'US',
};
const JAZZ_STREAM = 'http://jking.cdnstream1.com/b22139_128mp3';
const PLAYING = 'Playing **101 SMOOTH JAZZ** in <#400>.';
const signal = new AbortController().signal;
const defer = vi.fn(async () => {});
const invocation = { guildId: '300', userId: '200', defer };
const fetchMock = vi.fn<typeof fetch>();
const play = vi.fn();
const stop = vi.fn();
const env = {
  DISCORD_BOT_TOKEN: 'bot-token',
  RADIO_PLAYER: { getByName: vi.fn(() => ({ play, stop })) },
} as unknown as Env;
let stations: object[];
let tagged: object[];
let streamName: string;
let voiceChannel: string | null;
let nextId = 0;

function requested(path: string) {
  return fetchMock.mock.calls
    .map(([url]) => new URL(String(url)))
    .filter((url) => url.pathname === path);
}

function command(subcommand: string, options: object[] = []) {
  return {
    id: '1',
    type: ApplicationCommandType.ChatInput,
    name: 'radio',
    options: [{ type: ApplicationCommandOptionType.Subcommand, name: subcommand, options }],
  } as APIChatInputApplicationCommandInteractionData;
}

function playCommand(station: string) {
  return command('play', [
    { type: ApplicationCommandOptionType.String, name: 'station', value: station },
  ]);
}

function interaction(type: InteractionType, data: object) {
  return {
    type,
    id: snowflake(Date.now(), nextId++),
    application_id: '900',
    token: 'interaction-token',
    guild_id: '300',
    member: { user: { id: '200' } },
    data,
  } as unknown as APIInteraction;
}

function callbacks() {
  return fetchMock.mock.calls
    .filter(([url]) => String(url).endsWith('/callback'))
    .map(([, init]) => JSON.parse(init!.body as string));
}

function edits() {
  return fetchMock.mock.calls
    .filter(([url]) => String(url).endsWith('/messages/%40original'))
    .map(([, init]) => JSON.parse((init!.body as FormData).get('payload_json') as string));
}

beforeEach(() => {
  stations = [JAZZ];
  tagged = [];
  streamName = JAZZ.name;
  voiceChannel = '400';
  defer.mockClear();
  play.mockReset().mockResolvedValue(undefined);
  stop.mockReset();
  fetchMock.mockReset().mockImplementation(async (input) => {
    const url = new URL(String(input));
    if (url.pathname === '/json/stations/search') {
      return Response.json(url.searchParams.has('tag') ? tagged : stations);
    }
    if (url.pathname === `/json/url/${JAZZ.stationuuid}`) {
      return Response.json({ ok: true, name: streamName, url: JAZZ_STREAM });
    }
    if (url.pathname === '/api/v10/guilds/300/voice-states/200') {
      return voiceChannel
        ? Response.json({ channel_id: voiceChannel })
        : new Response(null, { status: 404 });
    }
    if (url.pathname.endsWith('/callback')) return new Response(null, { status: 204 });
    if (url.pathname.endsWith('/messages/%40original')) return Response.json({ id: 'original' });
    return new Response('Not found', { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('findStation', () => {
  it('looks up a station by name and asks Radio Browser for its stream, which counts the play', async () => {
    expect(await findStation('jazz', signal)).toEqual({
      name: '101 SMOOTH JAZZ',
      url: JAZZ_STREAM,
    });

    const [search] = requested('/json/stations/search');
    expect(Object.fromEntries(search!.searchParams)).toEqual({
      name: 'jazz',
      limit: '1',
      hidebroken: 'true',
      order: 'clickcount',
      reverse: 'true',
    });
    expect(requested(`/json/url/${JAZZ.stationuuid}`)).toHaveLength(1);
  });

  it('takes a station ID from autocomplete without searching', async () => {
    expect(await findStation(JAZZ.stationuuid, signal)).toEqual({
      name: '101 SMOOTH JAZZ',
      url: JAZZ_STREAM,
    });
    expect(requested('/json/stations/search')).toEqual([]);
  });

  it('plays a pasted stream URL as is', async () => {
    expect(await findStation('https://ice.example.com:8443/jazz.mp3', signal)).toEqual({
      name: 'ice.example.com:8443',
      url: 'https://ice.example.com:8443/jazz.mp3',
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([
    [
      'a name with no station',
      'nothing like this',
      `I couldn't find a station called "nothing like this".`,
    ],
    [
      'an unknown station ID',
      '00000000-0000-0000-0000-000000000000',
      `I couldn't find a station called "00000000-0000-0000-0000-000000000000".`,
    ],
    ['a broken URL', 'http://', "That isn't a valid stream URL."],
  ])('explains %s', async (_, input, message) => {
    stations = [];

    await expect(findStation(input, signal)).rejects.toEqual(new CommandError(message));
  });

  it('explains when the directory is down or too slow', async () => {
    fetchMock.mockImplementationOnce(async () => new Response('Down', { status: 502 }));

    await expect(findStation('jazz', signal)).rejects.toEqual(
      new CommandError("I couldn't reach the station directory. Try again in a moment."),
    );
    expect(console.error).toHaveBeenCalledWith(
      'Station lookup failed',
      'Radio Browser returned HTTP 502',
    );
  });
});

describe('stationChoices', () => {
  it('labels each station with its country and quality, and fills in its ID', async () => {
    expect(await stationChoices('jazz', signal)).toEqual([
      { name: '101 SMOOTH JAZZ · US · MP3 · 192 kbps', value: JAZZ.stationuuid },
    ]);
    expect(requested('/json/stations/search')[0]!.searchParams.get('limit')).toBe('25');
  });

  it('shortens long names to fit Discord, keeping the details', async () => {
    stations = [{ ...JAZZ, name: 'Jazz '.repeat(30), bitrate: 0, codec: '' }];

    const [choice] = await stationChoices('jazz', signal);

    expect(choice!.name).toHaveLength(100);
    expect(choice!.name).toMatch(/^Jazz Jazz .*… · US$/);
  });
});

describe('searchStations', () => {
  function station(id: number, clickcount: number, extra: object = {}) {
    return {
      stationuuid: `station-${id}`,
      name: ` Station ${id} `,
      codec: 'MP3',
      bitrate: 128,
      countrycode: 'US',
      tags: 'classic rock,rock,70s,80s,90s,oldies,hits',
      votes: 1_000 + id,
      clickcount,
      ...extra,
    };
  }

  it('finds stations by name and by genre, most played first, without repeats', async () => {
    stations = [station(1, 50), station(2, 10)];
    tagged = [station(3, 90, { codec: 'AAC', bitrate: 0, tags: '' }), station(1, 50)];

    expect(await searchStations('classic rock', signal)).toEqual([
      { id: 'station-3', name: 'Station 3', country: 'US', quality: 'AAC', tags: [], votes: 1_003 },
      {
        id: 'station-1',
        name: 'Station 1',
        country: 'US',
        quality: 'MP3 128 kbps',
        tags: ['classic rock', 'rock', '70s', '80s', '90s', 'oldies'],
        votes: 1_001,
      },
      expect.objectContaining({ id: 'station-2' }),
    ]);
    expect(
      requested('/json/stations/search').map((url) => [
        url.searchParams.get('name'),
        url.searchParams.get('tag'),
      ]),
    ).toEqual([
      ['classic rock', null],
      [null, 'classic rock'],
    ]);
  });

  it('keeps the eight most played', async () => {
    stations = Array.from({ length: 8 }, (_, i) => station(i, i));
    tagged = Array.from({ length: 8 }, (_, i) => station(i + 8, i + 8));

    const results = await searchStations('rock', signal);

    expect(results.map((result) => result.id)).toEqual(
      [15, 14, 13, 12, 11, 10, 9, 8].map((id) => `station-${id}`),
    );
  });
});

describe('/radio', () => {
  it("plays the station in the caller's voice channel, deferring only once the checks pass", async () => {
    const response = await radio.execute(playCommand('jazz'), env, signal, invocation);

    expect(play).toHaveBeenCalledExactlyOnceWith('300', '400', {
      name: '101 SMOOTH JAZZ',
      url: JAZZ_STREAM,
    });
    expect(defer).toHaveBeenCalledOnce();
    expect(defer.mock.invocationCallOrder[0]).toBeLessThan(play.mock.invocationCallOrder[0]!);
    expect(response.data).toEqual({ content: PLAYING, allowed_mentions: { parse: [] } });
  });

  it('escapes markdown in station names', async () => {
    streamName = '*Smooth* Jazz_FM';

    const response = await radio.execute(playCommand('jazz'), env, signal, invocation);

    expect(response.data.content).toBe('Playing **\\*Smooth\\* Jazz\\_FM** in <#400>.');
  });

  it('asks the caller to join a voice channel first', async () => {
    voiceChannel = null;

    await expect(radio.execute(playCommand('jazz'), env, signal, invocation)).rejects.toEqual(
      new CommandError('Join a voice channel first.'),
    );
    expect(defer).not.toHaveBeenCalled();
    expect(play).not.toHaveBeenCalled();
  });

  it("passes on why the radio couldn't play", async () => {
    play.mockResolvedValue("That station isn't streaming right now.");

    await expect(radio.execute(playCommand('jazz'), env, signal, invocation)).rejects.toEqual(
      new CommandError("That station isn't streaming right now."),
    );
  });

  it.each([
    [true, 'Stopped the radio.'],
    [false, "The radio isn't playing."],
  ])('stops the radio (playing: %s)', async (playing, content) => {
    stop.mockResolvedValue(playing);

    const response = await radio.execute(command('stop'), env, signal, invocation);

    expect(defer).toHaveBeenCalledOnce();
    expect(response.data.content).toBe(content);
  });

  it('only works in a server', async () => {
    await expect(radio.execute(command('stop'), env, signal, { userId: '200' })).rejects.toEqual(
      new CommandError('Use /radio in a server.'),
    );
  });
});

describe('/radio in Discord', () => {
  function run(station: string) {
    return handleInteraction(
      interaction(InteractionType.ApplicationCommand, playCommand(station)),
      env,
      sqlStorage(),
    );
  }

  it('replies privately, without "thinking", when the caller is not in a voice channel', async () => {
    voiceChannel = null;

    await run('jazz');

    expect(callbacks()).toEqual([
      {
        type: InteractionResponseType.ChannelMessageWithSource,
        data: { content: 'Join a voice channel first.', flags: MessageFlags.Ephemeral },
      },
    ]);
    expect(edits()).toEqual([]);
  });

  it('shows "thinking" once the checks pass, then edits in what is playing', async () => {
    await run('jazz');

    expect(callbacks()).toEqual([
      { type: InteractionResponseType.DeferredChannelMessageWithSource },
    ]);
    expect(edits()).toEqual([
      { content: PLAYING, allowed_mentions: { parse: [] }, attachments: [] },
    ]);
  });
});

describe('/radio autocomplete', () => {
  function typing(value: string) {
    return interaction(InteractionType.ApplicationCommandAutocomplete, {
      id: '1',
      type: ApplicationCommandType.ChatInput,
      name: 'radio',
      options: [
        {
          type: ApplicationCommandOptionType.Subcommand,
          name: 'play',
          options: [
            { type: ApplicationCommandOptionType.String, name: 'station', value, focused: true },
          ],
        },
      ],
    });
  }

  it('suggests stations matching what the user has typed', async () => {
    await handleInteraction(typing(' jazz '), env, sqlStorage());

    expect(requested('/json/stations/search')[0]!.searchParams.get('name')).toBe('jazz');
    expect(callbacks()).toEqual([
      {
        type: InteractionResponseType.ApplicationCommandAutocompleteResult,
        data: {
          choices: [{ name: '101 SMOOTH JAZZ · US · MP3 · 192 kbps', value: JAZZ.stationuuid }],
        },
      },
    ]);
  });

  it('suggests nothing when Radio Browser fails', async () => {
    fetchMock.mockImplementationOnce(async () => new Response('Down', { status: 502 }));

    await handleInteraction(typing('jazz'), env, sqlStorage());

    expect(callbacks()).toEqual([
      { type: InteractionResponseType.ApplicationCommandAutocompleteResult, data: { choices: [] } },
    ]);
    expect(console.error).toHaveBeenCalledWith(
      'Autocomplete failed',
      'radio',
      'Radio Browser returned HTTP 502',
    );
  });
});
