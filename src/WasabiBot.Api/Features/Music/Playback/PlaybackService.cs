using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Options;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal class PlaybackService(
    IAudioService audioService,
    RadioTrackMetadataStore radioTrackMetadataStore,
    ILogger<WasabiQueuedLavalinkPlayer> queuedPlayerLogger,
    IMusicPlaybackStatsRecorder musicPlaybackStatsRecorder,
    IMusicInactivityTracker? musicInactivityTracker = null)
{
    private readonly IAudioService _audioService = audioService;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;
    private readonly IMusicInactivityTracker _musicInactivityTracker = musicInactivityTracker ?? NullMusicInactivityTracker.Instance;
    private readonly PlayerFactory<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> _playerFactory
        = PlayerFactory.Create((IPlayerProperties<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> properties)
            => new WasabiQueuedLavalinkPlayer(properties, queuedPlayerLogger, musicPlaybackStatsRecorder, musicInactivityTracker ?? NullMusicInactivityTracker.Instance));

    public async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> RetrievePlaybackPlayerAsync(
        ICommandContext ctx,
        CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return (null, GuildOnly());
        }

        return await RetrievePlaybackPlayerAsync(ctx.GuildId.Value, ctx.UserVoiceChannelId, PlayerChannelBehavior.Join, cancellationToken);
    }

    public async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> RetrievePlaybackPlayerAsync(
        ulong guildId,
        ulong? voiceChannelId,
        PlayerChannelBehavior channelBehavior,
        CancellationToken cancellationToken)
    {
        var result = await _audioService.Players.RetrieveAsync(
            guildId,
            voiceChannelId,
            _playerFactory,
            Options.Create(new QueuedLavalinkPlayerOptions()),
            new PlayerRetrieveOptions(ChannelBehavior: channelBehavior),
            cancellationToken);

        if (result.IsSuccess && channelBehavior == PlayerChannelBehavior.Join && result.Player?.CurrentTrack is null)
        {
            _musicInactivityTracker.ScheduleIdleDisconnect(guildId);
        }

        return result.IsSuccess
            ? (result.Player, null)
            : (null, FromRetrieveStatus(result.Status));
    }

    public async Task<IQueuedLavalinkPlayer?> GetExistingPlayerAsync(ICommandContext ctx, CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return null;
        }

        return await GetExistingPlayerAsync(ctx.GuildId.Value, cancellationToken);
    }

    public async Task<IQueuedLavalinkPlayer?> GetExistingPlayerAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var player = await _audioService.Players.GetPlayerAsync(guildId, cancellationToken);
        return player as IQueuedLavalinkPlayer;
    }

    public async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetControllablePlayerAsync(
        ICommandContext ctx,
        CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return (null, GuildOnly());
        }

        return await RetrievePlaybackPlayerAsync(ctx.GuildId.Value, ctx.UserVoiceChannelId, PlayerChannelBehavior.None, cancellationToken);
    }

    public MusicCommandResult BuildQueuedTrackResult(LavalinkTrack track, int position)
    {
        return BuildQueuedDisplayResult(FormatTrack(track), position);
    }

    public static MusicCommandResult BuildQueuedDisplayResult(string display, int position)
    {
        var message = position == 0
            ? $"Now playing {display}."
            : $"Queued {display} at position {position}.";

        return new MusicCommandResult(message);
    }

    public MusicCommandResult BuildPlaylistQueuedResult(string playlistName, int trackCount, LavalinkTrack firstTrack)
    {
        return new MusicCommandResult(
            $"Queued {trackCount} tracks from **{playlistName}**. Starting with {FormatTrack(firstTrack)}.");
    }

    public static MusicCommandResult GuildOnly()
    {
        return new MusicCommandResult("Music commands can only be used in a server voice channel.", Ephemeral: true);
    }

    public static MusicCommandResult FromRetrieveStatus(PlayerRetrieveStatus status)
    {
        var message = status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => "You need to join a voice channel first.",
            PlayerRetrieveStatus.VoiceChannelMismatch => "You need to be in the same voice channel as the bot.",
            PlayerRetrieveStatus.UserInSameVoiceChannel => "I'm already connected to your voice channel.",
            PlayerRetrieveStatus.BotNotConnected => "I'm not connected to a voice channel right now.",
            PlayerRetrieveStatus.PreconditionFailed => "That action isn't allowed in the player's current state.",
            _ => "I couldn't access the music player right now."
        };

        return new MusicCommandResult(message, Ephemeral: true);
    }

    public string FormatTrack(LavalinkTrack? track)
    {
        var snapshot = CreateTrackSnapshot(track);
        if (snapshot is null)
        {
            return "an unknown track";
        }

        if (snapshot.IsRadio)
        {
            return $"**{snapshot.Title}** (`{snapshot.DurationText}`)";
        }

        return $"**{snapshot.Title}** by **{snapshot.Author}** (`{snapshot.DurationText}`)";
    }

    public MusicTrackSnapshot? CreateTrackSnapshot(LavalinkTrack? track)
    {
        if (track is null)
        {
            return null;
        }

        if (_radioTrackMetadataStore.TryGet(track, out var metadata))
        {
            return new MusicTrackSnapshot(
                metadata.Title,
                string.Empty,
                "LIVE",
                Duration: null,
                IsLive: true,
                IsRadio: true,
                ArtworkUrl: null,
                SourceUrl: track.Uri?.ToString(),
                SourceName: track.SourceName);
        }

        var duration = track.IsLiveStream ? "LIVE" : FormatDuration(track.Duration);
        return new MusicTrackSnapshot(
            track.Title,
            track.Author,
            duration,
            track.IsLiveStream ? null : track.Duration,
            track.IsLiveStream,
            IsRadio: false,
            ArtworkUrl: track.ArtworkUri?.ToString(),
            SourceUrl: track.Uri?.ToString(),
            SourceName: track.SourceName);
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}
