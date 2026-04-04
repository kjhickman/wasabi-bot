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
    RadioTrackMetadataStore radioTrackMetadataStore) : IMusicDashboardQueueService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly IAudioService _audioService = audioService;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;

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

    private async Task<MusicCommandResult> QueueTrackCoreAsync(ulong userId, LavalinkTrack track, bool playNext, CancellationToken cancellationToken)
    {
        var playerResult = await GetPlayerAsync(userId, cancellationToken);
        if (playerResult.Result is not null)
        {
            return playerResult.Result;
        }

        var player = playerResult.Player!;
        if (playNext && (player.CurrentTrack is not null || player.Queue.Count > 0))
        {
            await player.Queue.InsertAsync(0, new TrackQueueItem(track), cancellationToken);
            return new MusicCommandResult($"Queued {_playbackService.FormatTrack(track)} to play next.");
        }

        var position = await player.PlayAsync(track, enqueue: true, cancellationToken: cancellationToken);
        return _playbackService.BuildQueuedTrackResult(track, position);
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

    private async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetPlayerAsync(ulong userId, CancellationToken cancellationToken)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return (null, new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true));
        }

        var existingPlayer = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        if (existingPlayer is not null)
        {
            return (existingPlayer, null);
        }

        return await _playbackService.RetrievePlaybackPlayerAsync(
            sharedVoiceChannel.GuildId,
            sharedVoiceChannel.VoiceChannelId,
            PlayerChannelBehavior.Join,
            cancellationToken);
    }
}
