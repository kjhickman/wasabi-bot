using Lavalink4NET;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicService(IAudioService audioService, PlaybackService playbackService, ILogger<MusicService> logger, Tracer tracer) : IMusicService
{
    private const int QueuePreviewLimit = 10;

    private readonly IAudioService _audioService = audioService;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly ILogger<MusicService> _logger = logger;
    private readonly Tracer _tracer = tracer;

    public async Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string input, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.play");
        AddContextAttributes(span, ctx);
        span.SetAttribute("music.input.is_url", Uri.TryCreate(input.Trim(), UriKind.Absolute, out _));

        if (!ctx.GuildId.HasValue)
        {
            span.SetAttribute("music.result", "guild_only");
            return PlaybackService.GuildOnly();
        }

        var identifier = input.Trim();
        var trackLoad = await LoadTrackAsync(identifier, cancellationToken);
        if (trackLoad.Result is not null)
        {
            span.SetAttribute("music.result", "track_load_failed");
            return trackLoad.Result;
        }

        if (trackLoad.LoadResult is not { } loadResult)
        {
            span.SetAttribute("music.result", "track_load_unavailable");
            return new MusicCommandResult("Lavalink couldn't load that track right now. Please try again later.", Ephemeral: true);
        }

        var (lavalinkPlayer, result) = await _playbackService.RetrievePlaybackPlayerAsync(ctx, cancellationToken);
        if (result is not null)
        {
            span.SetAttribute("music.player_retrieve_status", "failed");
            return result;
        }
        if (lavalinkPlayer is null)
        {
            return new MusicCommandResult("Lavalink isn't available right now. Please try again later.", Ephemeral: true);
        }

        if (loadResult.IsPlaylist)
        {
            span.SetAttribute("music.load_type", "playlist");
            span.SetAttribute("music.track_count", loadResult.Tracks.Length);
            var firstTrack = loadResult.Tracks[0];
            for (var index = 0; index < loadResult.Tracks.Length; index++)
            {
                await lavalinkPlayer.PlayAsync(loadResult.Tracks[index], enqueue: true, cancellationToken: cancellationToken);
            }

            return _playbackService.BuildPlaylistQueuedResult(loadResult.Playlist!.Name, loadResult.Tracks.Length, firstTrack);
        }

        var track = loadResult.Track!;
        span.SetAttribute("music.load_type", "track");
        span.SetAttribute("music.track.title", track.Title);
        span.SetAttribute("music.track.author", track.Author);
        var position = await lavalinkPlayer.PlayAsync(track, enqueue: true, cancellationToken: cancellationToken);
        span.SetAttribute("music.queue_position", position);
        return _playbackService.BuildQueuedTrackResult(track, position);
    }

    private async Task<(TrackLoadResult? LoadResult, MusicCommandResult? Result)> LoadTrackAsync(
        string input,
        CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("music.load-track");
        span.SetAttribute("music.input.length", input.Length);

        var identifier = input.Trim();
        span.SetAttribute("music.search_attempts", 1);
        TrackException? lastException = null;

        var isUrl = Uri.TryCreate(identifier, UriKind.Absolute, out _);
        span.SetAttribute("music.search_mode", isUrl ? "url" : TrackSearchMode.SoundCloud.Prefix);
        var loadResult = await _audioService.Tracks.LoadTracksAsync(
            identifier,
            isUrl
                ? new TrackLoadOptions(SearchBehavior: StrictSearchBehavior.Passthrough)
                : new TrackLoadOptions(SearchMode: TrackSearchMode.SoundCloud, SearchBehavior: StrictSearchBehavior.Explicit),
            cancellationToken: cancellationToken);

        if (loadResult.HasMatches)
        {
            span.SetAttribute("music.search_match_count", loadResult.Tracks.Length);
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
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            span.SetAttribute("music.result", "player_unavailable");
            return player.Result;
        }

        if (player.Player!.CurrentTrack is null && player.Player.Queue.Count == 0)
        {
            span.SetAttribute("music.result", "nothing_playing");
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        await player.Player.SkipAsync(cancellationToken: cancellationToken);
        span.SetAttribute("music.result", "skipped");
        return new MusicCommandResult("Skipped the current track.");
    }

    public async Task<MusicCommandResult> StopAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.stop");
        AddContextAttributes(span, ctx);
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            span.SetAttribute("music.result", "player_unavailable");
            return player.Result;
        }

        await player.Player!.StopAsync(cancellationToken);
        span.SetAttribute("music.result", "stopped");
        return new MusicCommandResult("Stopped playback and cleared the queue.");
    }

    public async Task<MusicCommandResult> QueueAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.queue");
        AddContextAttributes(span, ctx);
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player is null || (player.CurrentTrack is null && player.Queue.Count == 0))
        {
            span.SetAttribute("music.result", "empty_queue");
            return new MusicCommandResult("The queue is currently empty.", Ephemeral: true);
        }

        span.SetAttribute("music.queue_count", player.Queue.Count);
        return new MusicCommandResult(BuildQueueMessage(player));
    }

    public async Task<MusicCommandResult> NowPlayingAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.now-playing");
        AddContextAttributes(span, ctx);
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player?.CurrentTrack is null)
        {
            span.SetAttribute("music.result", "nothing_playing");
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        var currentTrack = player.CurrentTrack;
        var queueSuffix = player.Queue.Count > 0
            ? $" {player.Queue.Count} more track(s) queued."
            : string.Empty;

        span.SetAttribute("music.queue_count", player.Queue.Count);
        return new MusicCommandResult($"Now playing {_playbackService.FormatTrack(currentTrack)}.{queueSuffix}");
    }

    public async Task<MusicCommandResult> LeaveAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.leave");
        AddContextAttributes(span, ctx);
        var (player, result) = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (result is not null)
        {
            span.SetAttribute("music.result", "player_unavailable");
            return result;
        }

        await player!.StopAsync(cancellationToken);
        await player.DisconnectAsync(cancellationToken);
        span.SetAttribute("music.result", "disconnected");
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
        span.SetAttribute("discord.user_id", ctx.UserId.ToString());
        span.SetAttribute("discord.channel_id", ctx.ChannelId.ToString());

        if (ctx.GuildId.HasValue)
        {
            span.SetAttribute("discord.guild_id", ctx.GuildId.Value.ToString());
        }

        if (ctx.UserVoiceChannelId.HasValue)
        {
            span.SetAttribute("discord.voice_channel_id", ctx.UserVoiceChannelId.Value.ToString());
        }
    }
}
