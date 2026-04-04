namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardService(
    ISharedVoiceChannelResolver sharedVoiceChannelResolver,
    PlaybackService playbackService) : IMusicDashboardService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;

    public async Task<ActiveMusicSession?> GetActiveSessionAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return null;
        }

        var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        return new ActiveMusicSession(
            sharedVoiceChannel,
            _playbackService.CreateTrackSnapshot(player?.CurrentTrack),
            BuildQueue(player));
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
