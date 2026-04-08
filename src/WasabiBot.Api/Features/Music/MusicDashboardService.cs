using Lavalink4NET.Players;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardService(
    ISharedVoiceChannelResolver sharedVoiceChannelResolver,
    PlaybackService playbackService) : IMusicDashboardService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;

    public async Task<ActiveMusicSession?> GetActiveSessionAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var userVoiceChannel = _sharedVoiceChannelResolver.ResolveUserVoiceChannel(userId);
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return userVoiceChannel is null
                ? null
                : new ActiveMusicSession(
                    new SharedVoiceChannel(userVoiceChannel.GuildId, userVoiceChannel.GuildName, userVoiceChannel.VoiceChannelId, userVoiceChannel.VoiceChannelName),
                    "Idle",
                    null,
                    null,
                    [],
                    userVoiceChannel);
        }

        var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        return new ActiveMusicSession(
            sharedVoiceChannel,
            BuildPlaybackState(player),
            BuildProgress(player),
            _playbackService.CreateTrackSnapshot(player?.CurrentTrack),
            BuildQueue(player),
            userVoiceChannel);
    }

    private static string BuildPlaybackState(Lavalink4NET.Players.Queued.IQueuedLavalinkPlayer? player)
    {
        if (player?.CurrentTrack is null)
        {
            return "Idle";
        }

        return player.State switch
        {
            PlayerState.Paused => "Paused",
            PlayerState.Playing => "Playing",
            _ => "Idle"
        };
    }

    private PlaybackProgressSnapshot? BuildProgress(Lavalink4NET.Players.Queued.IQueuedLavalinkPlayer? player)
    {
        var track = _playbackService.CreateTrackSnapshot(player?.CurrentTrack);
        if (player is null || track is null || track.IsLive)
        {
            return null;
        }

        var position = player.Position?.Position ?? TimeSpan.Zero;
        double? percent = track.Duration is { } duration && duration > TimeSpan.Zero
            ? Math.Clamp(position.TotalMilliseconds / duration.TotalMilliseconds * 100D, 0D, 100D)
            : null;

        return new PlaybackProgressSnapshot(position, PlaybackService.FormatDuration(position), percent);
    }

    private IReadOnlyList<MusicQueueItemSnapshot> BuildQueue(Lavalink4NET.Players.Queued.IQueuedLavalinkPlayer? player)
    {
        if (player is null || player.Queue.Count == 0)
        {
            return [];
        }

        var queue = new List<MusicQueueItemSnapshot>(player.Queue.Count);
        for (var index = 0; index < player.Queue.Count; index++)
        {
            var track = _playbackService.CreateTrackSnapshot(player.Queue[index].Track);
            if (track is null)
            {
                continue;
            }

            queue.Add(new MusicQueueItemSnapshot(index + 1, track));
        }

        return queue;
    }
}
