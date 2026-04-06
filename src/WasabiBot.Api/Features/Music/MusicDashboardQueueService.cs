using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardQueueService(
    ISharedVoiceChannelResolver sharedVoiceChannelResolver,
    PlaybackService playbackService,
    IAudioService audioService,
    RadioTrackMetadataStore radioTrackMetadataStore,
    IMusicQueueMutationCoordinator queueMutationCoordinator) : IMusicDashboardQueueService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly IAudioService _audioService = audioService;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;
    private readonly IMusicQueueMutationCoordinator _queueMutationCoordinator = queueMutationCoordinator;

    public Task<MusicCommandResult> QueueTrackAsync(ulong userId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        return QueueTrackCoreAsync(userId, track, playNext: false, cancellationToken);
    }

    public Task<MusicCommandResult> PlayNextTrackAsync(ulong userId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        return QueueTrackCoreAsync(userId, track, playNext: true, cancellationToken);
    }

    public async Task<MusicCommandResult> QueueStationAsync(ulong userId, RadioBrowserStation station, CancellationToken cancellationToken = default)
    {
        var trackLoad = await LoadStationTrackAsync(station, cancellationToken);
        return trackLoad.Result is not null
            ? trackLoad.Result
            : await QueueTrackCoreAsync(userId, trackLoad.Track!, playNext: false, cancellationToken);
    }

    public async Task<MusicCommandResult> PlayNextStationAsync(ulong userId, RadioBrowserStation station, CancellationToken cancellationToken = default)
    {
        var trackLoad = await LoadStationTrackAsync(station, cancellationToken);
        return trackLoad.Result is not null
            ? trackLoad.Result
            : await QueueTrackCoreAsync(userId, trackLoad.Track!, playNext: true, cancellationToken);
    }

    public async Task<MusicCommandResult> RemoveQueueItemAsync(ulong userId, int position, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = ResolveSharedVoiceChannel(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, ct);
            if (player is null)
            {
                return new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true);
            }

            if (!TryGetQueueItem(player, position, out var queueItem, out var invalidPositionResult))
            {
                return invalidPositionResult!;
            }

            await player.Queue.RemoveAtAsync(position - 1, ct);
            return new MusicCommandResult($"Removed {_playbackService.FormatTrack(queueItem!.Track)} from the queue.");
        }, cancellationToken);
    }

    public async Task<MusicCommandResult> MoveQueueItemAsync(ulong userId, int position, int newPosition, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = ResolveSharedVoiceChannel(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, ct);
            if (player is null)
            {
                return new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true);
            }

            if (!TryGetQueueItem(player, position, out var queueItem, out var invalidPositionResult))
            {
                return invalidPositionResult!;
            }

            if (newPosition < 1 || newPosition > player.Queue.Count)
            {
                return new MusicCommandResult("That move is no longer valid because the queue changed.", Ephemeral: true);
            }

            if (position == newPosition)
            {
                return new MusicCommandResult("That track is already in that spot.", Ephemeral: true);
            }

            await player.Queue.RemoveAtAsync(position - 1, ct);
            await player.Queue.InsertAsync(newPosition - 1, new TrackQueueItem(queueItem!.Track!), ct);
            return new MusicCommandResult($"Moved {_playbackService.FormatTrack(queueItem.Track)} to position {newPosition}.");
        }, cancellationToken);
    }

    public async Task<MusicCommandResult> ClearQueueAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = ResolveSharedVoiceChannel(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, ct);
            if (player is null)
            {
                return new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true);
            }

            if (player.Queue.Count == 0)
            {
                return new MusicCommandResult("The queue is already empty.", Ephemeral: true);
            }

            await player.Queue.ClearAsync(ct);
            return new MusicCommandResult("Cleared the queued tracks.");
        }, cancellationToken);
    }

    private async Task<MusicCommandResult> QueueTrackCoreAsync(ulong userId, LavalinkTrack track, bool playNext, CancellationToken cancellationToken)
    {
        var sharedVoiceChannel = ResolveSharedVoiceChannel(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var playerResult = await RetrievePlayerAsync(sharedVoiceChannel, ct);
            if (playerResult.Result is not null)
            {
                return playerResult.Result;
            }

            var player = playerResult.Player!;
            if (playNext && (player.CurrentTrack is not null || player.Queue.Count > 0))
            {
                await player.Queue.InsertAsync(0, new TrackQueueItem(track), ct);
                return new MusicCommandResult($"Queued {_playbackService.FormatTrack(track)} to play next.");
            }

            var position = await player.PlayAsync(track, enqueue: true, cancellationToken: ct);
            return _playbackService.BuildQueuedTrackResult(track, position);
        }, cancellationToken);
    }

    private async Task<(LavalinkTrack? Track, MusicCommandResult? Result)> LoadStationTrackAsync(RadioBrowserStation station, CancellationToken cancellationToken)
    {
        var loadResult = await _audioService.Tracks.LoadTracksAsync(
            station.UrlResolved,
            new TrackLoadOptions(SearchBehavior: StrictSearchBehavior.Passthrough),
            cancellationToken: cancellationToken);

        if (!loadResult.HasMatches)
        {
            return (null, new MusicCommandResult("That radio station stream isn't playable right now.", Ephemeral: true));
        }

        var track = loadResult.Track ?? loadResult.Tracks[0];
        _radioTrackMetadataStore.Set(track, station.Name);
        return (track, null);
    }

    private async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> RetrievePlayerAsync(SharedVoiceChannel sharedVoiceChannel, CancellationToken cancellationToken)
    {
        var existingPlayer = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        if (existingPlayer is not null)
        {
            return (existingPlayer, null);
        }

        var player = await _playbackService.RetrievePlaybackPlayerAsync(
            sharedVoiceChannel.GuildId,
            sharedVoiceChannel.VoiceChannelId,
            PlayerChannelBehavior.Join,
            cancellationToken);

        return (player.Player, player.Result);
    }

    private SharedVoiceChannel? ResolveSharedVoiceChannel(ulong userId)
    {
        return _sharedVoiceChannelResolver.ResolveForUser(userId);
    }

    private static bool TryGetQueueItem(IQueuedLavalinkPlayer player, int position, out ITrackQueueItem? queueItem, out MusicCommandResult? result)
    {
        if (position < 1 || position > player.Queue.Count)
        {
            queueItem = null;
            result = new MusicCommandResult("That queued track no longer exists.", Ephemeral: true);
            return false;
        }

        queueItem = player.Queue[position - 1];
        result = null;
        return true;
    }
}
