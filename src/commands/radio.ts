import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  InteractionContextType,
  Routes,
  type APIApplicationCommandInteractionDataOption,
  type RESTGetAPIGuildVoiceStateUserResult,
} from 'discord-api-types/v10';
import { discordFetch } from '../discord.ts';
import type { Env } from '../env.ts';
import { findStation, stationChoices } from '../radio/stations.ts';
import { CommandError, reply, type SlashCommand } from './command.ts';

export const radio = {
  definition: {
    name: 'radio',
    description: 'Play internet radio in your voice channel.',
    type: ApplicationCommandType.ChatInput,
    contexts: [InteractionContextType.Guild],
    options: [
      {
        type: ApplicationCommandOptionType.Subcommand,
        name: 'play',
        description: 'Play a station in your voice channel, or switch stations.',
        options: [
          {
            type: ApplicationCommandOptionType.String,
            name: 'station',
            description: 'A station name or stream URL',
            required: true,
            autocomplete: true,
          },
        ],
      },
      {
        type: ApplicationCommandOptionType.Subcommand,
        name: 'stop',
        description: 'Stop the radio and leave the voice channel.',
      },
    ],
  },
  autocomplete(data, _env, signal) {
    const typed = subcommandOptions(data.options).find(
      (option) => 'focused' in option && option.focused,
    );
    return stationChoices(String(typed?.value ?? '').trim(), signal);
  },

  // Checks what it can within Discord's 3-second window, so mistakes get a private reply,
  // then defers: starting a container and joining voice take longer.
  async execute(data, env, signal, { guildId, userId, defer }) {
    const [subcommand] = data.options ?? [];
    if (!guildId || !userId || subcommand?.type !== ApplicationCommandOptionType.Subcommand) {
      throw new CommandError('Use /radio in a server.');
    }
    const player = env.RADIO_PLAYER.getByName(guildId);
    if (subcommand.name === 'stop') {
      await defer?.();
      const stopped = await player.stop();
      return reply({ content: stopped ? 'Stopped the radio.' : "The radio isn't playing." });
    }

    const input = subcommandOptions(data.options).find((option) => option.name === 'station');
    const channelId = await voiceChannel(env, guildId, userId, signal);
    const station = await findStation(String(input?.value ?? '').trim(), signal);
    await defer?.();
    const reason = await player.play(guildId, channelId, station);
    if (reason) throw new CommandError(reason);
    return reply({
      content: `Playing **${escapeMarkdown(station.name)}** in <#${channelId}>.`,
      allowed_mentions: { parse: [] },
    });
  },
} satisfies SlashCommand;

function subcommandOptions(options: APIApplicationCommandInteractionDataOption[] = []) {
  const [subcommand] = options;
  return subcommand?.type === ApplicationCommandOptionType.Subcommand
    ? (subcommand.options ?? [])
    : [];
}

async function voiceChannel(env: Env, guildId: string, userId: string, signal: AbortSignal) {
  const response = await discordFetch(Routes.guildVoiceState(guildId, userId), {
    token: env.DISCORD_BOT_TOKEN ?? '',
    signal,
  });
  if (response.status === 404) throw new CommandError('Join a voice channel first.');
  if (!response.ok) throw new Error(`Voice state lookup failed: HTTP ${response.status}`);
  const { channel_id: channelId } = await response.json<RESTGetAPIGuildVoiceStateUserResult>();
  if (!channelId) throw new CommandError('Join a voice channel first.');
  return channelId;
}

function escapeMarkdown(text: string) {
  return text.replace(/[\\*_~`|]/g, '\\$&');
}
