using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicService(IAudioService audioService, ILogger<MusicService> logger, Tracer tracer) : IMusicService
{
    private const int QueuePreviewLimit = 10;
    private static readonly SearchAttempt[] SearchAttempts =
    [
        new(TrackSearchMode.YouTube),
        new(TrackSearchMode.SoundCloud),
    ];

    private readonly IAudioService _audioService = audioService;
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
            return GuildOnly();
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

        var playerResult = await _audioService.Players.RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
            ctx.GuildId.Value,
            ctx.UserVoiceChannelId,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions()),
            new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.Join),
            cancellationToken);

        if (!playerResult.IsSuccess)
        {
            span.SetAttribute("music.player_retrieve_status", playerResult.Status.ToString());
            return FromRetrieveStatus(playerResult.Status);
        }

        var player = playerResult.Player;
        if (loadResult.IsPlaylist)
        {
            span.SetAttribute("music.load_type", "playlist");
            span.SetAttribute("music.track_count", loadResult.Tracks.Length);
            var firstTrack = loadResult.Tracks[0];
            for (var index = 0; index < loadResult.Tracks.Length; index++)
            {
                await player.PlayAsync(loadResult.Tracks[index], enqueue: true, cancellationToken: cancellationToken);
            }

            return new MusicCommandResult(
                $"Queued {loadResult.Tracks.Length} tracks from **{loadResult.Playlist!.Name}**. Starting with {FormatTrack(firstTrack)}.");
        }

        var track = loadResult.Track!;
        span.SetAttribute("music.load_type", "track");
        span.SetAttribute("music.track.title", track.Title);
        span.SetAttribute("music.track.author", track.Author);
        var position = await player.PlayAsync(track, enqueue: true, cancellationToken: cancellationToken);
        span.SetAttribute("music.queue_position", position);
        var message = position == 0
            ? $"Now playing {FormatTrack(track)}."
            : $"Queued {FormatTrack(track)} at position {position}.";

        return new MusicCommandResult(message);
    }

    private async Task<(TrackLoadResult? LoadResult, MusicCommandResult? Result)> LoadTrackAsync(
        string input,
        CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("music.load-track");
        span.SetAttribute("music.input.length", input.Length);

        var attempts = BuildAttempts(input);
        span.SetAttribute("music.search_attempts", attempts.Count);
        TrackException? lastException = null;

        foreach (var attempt in attempts)
        {
            span.SetAttribute("music.search_mode", attempt.Options.SearchMode.Prefix ?? "url");
            var loadResult = await _audioService.Tracks.LoadTracksAsync(
                attempt.Identifier,
                attempt.Options,
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
                _logger.LogWarning("Lavalink failed to load track for identifier {Identifier}: {Exception}", attempt.Identifier, trackException.Message);
            }
        }

        if (lastException is not null)
        {
            return (null, new MusicCommandResult("Lavalink couldn't load that track right now. Please try again later.", Ephemeral: true));
        }

        var message = attempts.Count == 1
            ? "I couldn't find anything playable at that URL."
            : "I couldn't find anything playable for that search.";

        return (null, new MusicCommandResult(message, Ephemeral: true));
    }

    private static IReadOnlyList<SearchAttempt> BuildAttempts(string input)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out _))
        {
            return [new SearchAttempt(input, new TrackLoadOptions(SearchBehavior: StrictSearchBehavior.Passthrough))];
        }

        return SearchAttempts
            .Select(attempt => attempt with { Identifier = input })
            .ToArray();
    }

    private readonly record struct SearchAttempt(string Identifier, TrackLoadOptions Options)
    {
        public SearchAttempt(TrackSearchMode searchMode)
            : this(
                string.Empty,
                new TrackLoadOptions(SearchMode: searchMode, SearchBehavior: StrictSearchBehavior.Explicit))
        {
        }
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
        return new MusicCommandResult($"Now playing {FormatTrack(currentTrack)}.{queueSuffix}");
    }

    public async Task<MusicCommandResult> LeaveAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("music.leave");
        AddContextAttributes(span, ctx);
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            span.SetAttribute("music.result", "player_unavailable");
            return player.Result;
        }

        await player.Player!.StopAsync(cancellationToken);
        await player.Player.DisconnectAsync(cancellationToken);
        span.SetAttribute("music.result", "disconnected");
        return new MusicCommandResult("Left the voice channel.");
    }

    private async Task<IQueuedLavalinkPlayer?> GetExistingPlayerAsync(ICommandContext ctx, CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return null;
        }

        var player = await _audioService.Players.GetPlayerAsync(ctx.GuildId.Value, cancellationToken);
        return player as IQueuedLavalinkPlayer;
    }

    private async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetControllablePlayerAsync(
        ICommandContext ctx,
        CancellationToken cancellationToken)
    {
        if (!ctx.GuildId.HasValue)
        {
            return (null, GuildOnly());
        }

        var result = await _audioService.Players.RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
            ctx.GuildId.Value,
            ctx.UserVoiceChannelId,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions()),
            new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.None),
            cancellationToken);

        return result.IsSuccess
            ? (result.Player, null)
            : (null, FromRetrieveStatus(result.Status));
    }

    private static MusicCommandResult FromRetrieveStatus(PlayerRetrieveStatus status)
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

    private static MusicCommandResult GuildOnly()
    {
        return new MusicCommandResult("Music commands can only be used in a server voice channel.", Ephemeral: true);
    }

    private static string BuildQueueMessage(IQueuedLavalinkPlayer player)
    {
        var lines = new List<string>();

        if (player.CurrentTrack is not null)
        {
            lines.Add($"Now playing: {FormatTrack(player.CurrentTrack)}");
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
            lines.Add($"{index + 1}. {FormatTrack(track)}");
        }

        if (player.Queue.Count > QueuePreviewLimit)
        {
            lines.Add($"...and {player.Queue.Count - QueuePreviewLimit} more.");
        }

        return string.Join('\n', lines);
    }

    private static string FormatTrack(LavalinkTrack? track)
    {
        if (track is null)
        {
            return "an unknown track";
        }

        var duration = track.IsLiveStream ? "LIVE" : FormatDuration(track.Duration);
        return $"**{track.Title}** by **{track.Author}** (`{duration}`)";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
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
