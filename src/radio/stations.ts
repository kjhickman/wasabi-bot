import type { APIApplicationCommandOptionChoice } from 'discord-api-types/v10';
import { CommandError } from '../commands/command.ts';
import { log, preview } from '../log.ts';
import type { Station } from './player.ts';

const API_URL = 'https://all.api.radio-browser.info/json';
const HEADERS = { 'User-Agent': 'wasabi-bot/0.1' };
const STATION_ID = /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i;
const MAX_CHOICES = 25;
const MAX_CHOICE_LENGTH = 100;
const MAX_RESULTS = 8;
const MAX_TAGS = 6;

interface ApiStation {
  stationuuid: string;
  name: string;
  codec: string;
  bitrate: number;
  countrycode: string;
  tags: string;
  votes: number;
  clickcount: number;
}

async function radioBrowser<T>(path: string, signal: AbortSignal): Promise<T | undefined> {
  const response = await fetch(`${API_URL}${path}`, { headers: HEADERS, signal });
  if (response.status === 404) return undefined;
  if (!response.ok) throw new Error(`Radio Browser returned HTTP ${response.status}`);
  return response.json<T>();
}

async function search(by: { name: string } | { tag: string }, limit: number, signal: AbortSignal) {
  const params = new URLSearchParams({
    ...by,
    limit: String(limit),
    hidebroken: 'true',
    order: 'clickcount',
    reverse: 'true',
  });
  return (await radioBrowser<ApiStation[]>(`/stations/search?${params}`, signal)) ?? [];
}

export async function findStation(input: string, signal: AbortSignal): Promise<Station> {
  if (/^https?:\/\//i.test(input)) {
    const url = URL.parse(input);
    if (!url) throw new CommandError("That isn't a valid stream URL.");
    return { name: url.host, url: url.href };
  }
  let station: { ok: boolean; name: string; url: string } | undefined;
  try {
    const id = STATION_ID.test(input)
      ? input
      : (await search({ name: input }, 1, signal))[0]?.stationuuid;
    // Radio Browser counts a play each time an app asks for a station's URL this way.
    if (id) station = await radioBrowser(`/url/${id}`, signal);
  } catch (error) {
    log.error('Station lookup failed', { error, input: preview(input) });
    throw new CommandError("I couldn't reach the station directory. Try again in a moment.");
  }
  if (!station?.ok) throw new CommandError(`I couldn't find a station called "${input}".`);
  return { name: station.name.trim() || 'Unnamed station', url: station.url };
}

export async function stationChoices(
  query: string,
  signal: AbortSignal,
): Promise<APIApplicationCommandOptionChoice<string>[]> {
  const stations = await search({ name: query }, MAX_CHOICES, signal);
  return stations.map((station) => ({ name: label(station), value: station.stationuuid }));
}

export async function searchStations(query: string, signal: AbortSignal) {
  const matches = await Promise.all([
    search({ name: query }, MAX_RESULTS, signal),
    search({ tag: query }, MAX_RESULTS, signal),
  ]);
  const unique = new Map(matches.flat().map((station) => [station.stationuuid, station]));
  return [...unique.values()]
    .toSorted((a, b) => b.clickcount - a.clickcount)
    .slice(0, MAX_RESULTS)
    .map(({ stationuuid, name, countrycode, codec, bitrate, tags, votes }) => ({
      id: stationuuid,
      name: name.trim(),
      country: countrycode,
      quality: [codec, bitrate ? `${bitrate} kbps` : ''].filter(Boolean).join(' '),
      tags: tags.split(',').filter(Boolean).slice(0, MAX_TAGS),
      votes,
    }));
}

function label({ name, codec, bitrate, countrycode }: ApiStation) {
  const details = [countrycode, codec, bitrate ? `${bitrate} kbps` : '']
    .filter(Boolean)
    .join(' · ');
  const suffix = details ? ` · ${details}` : '';
  const title = name.trim().replace(/\s+/g, ' ') || 'Unnamed station';
  const room = MAX_CHOICE_LENGTH - suffix.length;
  return (title.length > room ? `${title.slice(0, room - 1)}…` : title) + suffix;
}
