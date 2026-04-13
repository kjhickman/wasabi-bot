using Lavalink4NET;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicService(
    IAudioService audioService,
    PlaybackService playbackService,
    ILogger<MusicService> logger,
    Tracer tracer,
    IMusicQueueMutationCoordinator queueMutationCoordinator,
    IMusicInactivityTracker? musicInactivityTracker = null) : IMusicService
{
    private const int QueuePreviewLimit = 10;

    private readonly IAudioService _audioService = audioService;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly ILogger<MusicService> _logger = logger;
    private readonly Tracer _tracer = tracer;
    private readonly IMusicQueueMutationCoordinator _queueMutationCoordinator = queueMutationCoordinator;
    private readonly IMusicInactivityTracker _musicInactivityTracker = musicInactivityTracker ?? NullMusicInactivityTracker.Instance;

    public async Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string input, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.play");
        AddContextAttributes(span, ctx);

        if (!ctx.GuildId.HasValue)
        {
            return PlaybackService.GuildOnly();
        }

        var identifier = input.Trim();
        var trackLoad = await LoadTrackAsync(identifier, cancellationToken);
        if (trackLoad.Result is not null)
        {
            return trackLoad.Result;
        }

        if (trackLoad.LoadResult is not { } loadResult)
        {
            return new MusicCommandResult("Lavalink couldn't load that track right now. Please try again later.", Ephemeral: true);
        }

        var guildId = ctx.GuildId.GetValueOrDefault();

        if (loadResult.IsPlaylist)
        {
            var firstTrack = loadResult.Tracks[0];

            var playlistResult = await _queueMutationCoordinator.ExecuteAsync(guildId, async ct =>
            {
                var (lavalinkPlayer, result) = await _playbackService.RetrievePlaybackPlayerAsync(ctx, ct);
                if (result is not null)
                {
                    return result;
                }

                if (lavalinkPlayer is null)
                {
                    return new MusicCommandResult("Lavalink isn't available right now. Please try again later.", Ephemeral: true);
                }

                for (var index = 0; index < loadResult.Tracks.Length; index++)
                {
                    await lavalinkPlayer.PlayAsync(loadResult.Tracks[index], enqueue: true, cancellationToken: ct);
                }

                return _playbackService.BuildPlaylistQueuedResult(loadResult.Playlist!.Name, loadResult.Tracks.Length, firstTrack);
            }, cancellationToken);

            return playlistResult;
        }

        var track = loadResult.Track!;
        var result = await _queueMutationCoordinator.ExecuteAsync(guildId, async ct =>
        {
            var (lavalinkPlayer, retrieveResult) = await _playbackService.RetrievePlaybackPlayerAsync(ctx, ct);
            if (retrieveResult is not null)
            {
                return (Position: (int?)null, Result: retrieveResult);
            }

            if (lavalinkPlayer is null)
            {
                return (Position: (int?)null, Result: new MusicCommandResult("Lavalink isn't available right now. Please try again later.", Ephemeral: true));
            }

            var position = await lavalinkPlayer.PlayAsync(track, enqueue: true, cancellationToken: ct);
            return (Position: (int?)position, Result: (MusicCommandResult?)null);
        }, cancellationToken);

        if (result.Result is not null)
        {
            return result.Result;
        }

        var position = result.Position!.Value;
        return _playbackService.BuildQueuedTrackResult(track, position);
    }

    private async Task<(TrackLoadResult? LoadResult, MusicCommandResult? Result)> LoadTrackAsync(
        string input,
        CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("music.load-track");

        var identifier = input.Trim();
        TrackException? lastException = null;

        var isUrl = Uri.TryCreate(identifier, UriKind.Absolute, out _);
        var loadResult = await _audioService.Tracks.LoadTracksAsync(
            identifier,
            isUrl
                ? new TrackLoadOptions(SearchBehavior: StrictSearchBehavior.Passthrough)
                : new TrackLoadOptions(SearchMode: TrackSearchMode.SoundCloud, SearchBehavior: StrictSearchBehavior.Explicit),
            cancellationToken: cancellationToken);

        if (loadResult.HasMatches)
        {
            return (loadResult, null);
        }

        if (loadResult.Exception is { } trackException)
        {
            lastException = trackException;
            span.RecordException(new InvalidOperationException(trackException.Message));
            _logger.LogWarning("Lavalink failed to load SoundCloud track for identifier {Identifier}: {Exception}", identifier, trackException.Message);
        }

        if (lastException is not null)
        {
            return (null, new MusicCommandResult("SoundCloud couldn't load that track right now. Please try again later.", Ephemeral: true));
        }

        return (null, new MusicCommandResult(
            isUrl
                ? "I couldn't find anything playable at that URL."
                : "I couldn't find anything playable on SoundCloud for that search.",
            Ephemeral: true));
    }

    public async Task<MusicCommandResult> SkipAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.skip");
        AddContextAttributes(span, ctx);
        if (!ctx.GuildId.HasValue)
        {
            return PlaybackService.GuildOnly();
        }

        var result = await _queueMutationCoordinator.ExecuteAsync(ctx.GuildId.GetValueOrDefault(), async ct =>
        {
            var player = await GetControllablePlayerAsync(ctx, ct);
            if (player.Result is not null)
            {
                return player.Result;
            }

            if (player.Player!.CurrentTrack is null && player.Player.Queue.Count == 0)
            {
                return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
            }

            await player.Player.SkipAsync(cancellationToken: ct);
            return new MusicCommandResult("Skipped the current track.");
        }, cancellationToken);

        return result;
    }

    public async Task<MusicCommandResult> StopAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.stop");
        AddContextAttributes(span, ctx);
        if (!ctx.GuildId.HasValue)
        {
            return PlaybackService.GuildOnly();
        }

        var result = await _queueMutationCoordinator.ExecuteAsync(ctx.GuildId.GetValueOrDefault(), async ct =>
        {
            var player = await GetControllablePlayerAsync(ctx, ct);
            if (player.Result is not null)
            {
                return player.Result;
            }

            await player.Player!.StopAsync(ct);
            _musicInactivityTracker.ScheduleIdleDisconnect(ctx.GuildId.GetValueOrDefault());
            return new MusicCommandResult("Stopped playback and cleared the queue.");
        }, cancellationToken);

        return result;
    }

    public async Task<MusicCommandResult> QueueAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.queue");
        AddContextAttributes(span, ctx);
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player is null || (player.CurrentTrack is null && player.Queue.Count == 0))
        {
            return new MusicCommandResult("The queue is currently empty.", Ephemeral: true);
        }

        return new MusicCommandResult(BuildQueueMessage(player));
    }

    public async Task<MusicCommandResult> NowPlayingAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.now-playing");
        AddContextAttributes(span, ctx);
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player?.CurrentTrack is null)
        {
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        var currentTrack = player.CurrentTrack;
        var queueSuffix = player.Queue.Count > 0
            ? $" {player.Queue.Count} more track(s) queued."
            : string.Empty;

        return new MusicCommandResult($"Now playing {_playbackService.FormatTrack(currentTrack)}.{queueSuffix}");
    }

    public async Task<MusicCommandResult> LeaveAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.leave");
        AddContextAttributes(span, ctx);
        if (!ctx.GuildId.HasValue)
        {
            return PlaybackService.GuildOnly();
        }

        var result = await _queueMutationCoordinator.ExecuteAsync(ctx.GuildId.GetValueOrDefault(), async ct =>
        {
            var (player, playerResult) = await GetControllablePlayerAsync(ctx, ct);
            if (playerResult is not null)
            {
                return (Player: (IQueuedLavalinkPlayer?)null, Result: playerResult);
            }

            await player!.StopAsync(ct);
            return (Player: player, Result: (MusicCommandResult?)null);
        }, cancellationToken);

        if (result.Result is not null)
        {
            return result.Result;
        }

        await result.Player!.DisconnectAsync(cancellationToken);
        _musicInactivityTracker.CancelDisconnect(ctx.GuildId.GetValueOrDefault());
        return new MusicCommandResult("Left the voice channel.");
    }

    private async Task<IQueuedLavalinkPlayer?> GetExistingPlayerAsync(ICommandContext ctx, CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return null;
        }

        return await _playbackService.GetExistingPlayerAsync(ctx, cancellationToken);
    }

    private async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetControllablePlayerAsync(
        ICommandContext ctx,
        CancellationToken cancellationToken)
    {
        return await _playbackService.GetControllablePlayerAsync(ctx, cancellationToken);
    }

    private string BuildQueueMessage(IQueuedLavalinkPlayer player)
    {
        var lines = new List<string>();

        if (player.CurrentTrack is not null)
        {
            lines.Add($"Now playing: {_playbackService.FormatTrack(player.CurrentTrack)}");
        }

        if (player.Queue.Count == 0)
        {
            lines.Add("Up next: nothing queued.");
            return string.Join('\n', lines);
        }

        lines.Add("Up next:");
        var count = Math.Min(player.Queue.Count, QueuePreviewLimit);
        for (var index = 0; index < count; index++)
        {
            var track = player.Queue[index].Track;
            lines.Add($"{index + 1}. {_playbackService.FormatTrack(track)}");
        }

        if (player.Queue.Count > QueuePreviewLimit)
        {
            lines.Add($"...and {player.Queue.Count - QueuePreviewLimit} more.");
        }

        return string.Join('\n', lines);
    }

    private static void AddContextAttributes(TelemetrySpan span, ICommandContext ctx)
    {
    }
}
