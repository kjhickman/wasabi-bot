using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Options;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicService(IAudioService audioService, ILogger<MusicService> logger) : IMusicService
{
    private const int QueuePreviewLimit = 10;
    private static readonly SearchAttempt[] SearchAttempts =
    [
        new(TrackSearchMode.YouTube),
        new(TrackSearchMode.SoundCloud),
    ];

    private readonly IAudioService _audioService = audioService;
    private readonly ILogger<MusicService> _logger = logger;

    public async Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string input, CancellationToken cancellationToken = default)
    {
        if (!ctx.GuildId.HasValue)
        {
            return GuildOnly();
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

        var playerResult = await _audioService.Players.RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
            ctx.GuildId.Value,
            ctx.UserVoiceChannelId,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions()),
            new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.Join),
            cancellationToken);

        if (!playerResult.IsSuccess)
        {
            return FromRetrieveStatus(playerResult.Status);
        }

        var player = playerResult.Player;
        if (loadResult.IsPlaylist)
        {
            var firstTrack = loadResult.Tracks[0];
            for (var index = 0; index < loadResult.Tracks.Length; index++)
            {
                await player.PlayAsync(loadResult.Tracks[index], enqueue: true, cancellationToken: cancellationToken);
            }

            return new MusicCommandResult(
                $"Queued {loadResult.Tracks.Length} tracks from **{loadResult.Playlist!.Name}**. Starting with {FormatTrack(firstTrack)}.");
        }

        var track = loadResult.Track!;
        var position = await player.PlayAsync(track, enqueue: true, cancellationToken: cancellationToken);
        var message = position == 0
            ? $"Now playing {FormatTrack(track)}."
            : $"Queued {FormatTrack(track)} at position {position}.";

        return new MusicCommandResult(message);
    }

    private async Task<(TrackLoadResult? LoadResult, MusicCommandResult? Result)> LoadTrackAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var attempts = BuildAttempts(input);
        TrackException? lastException = null;

        foreach (var attempt in attempts)
        {
            var loadResult = await _audioService.Tracks.LoadTracksAsync(
                attempt.Identifier,
                attempt.Options,
                cancellationToken: cancellationToken);

            if (loadResult.HasMatches)
            {
                return (loadResult, null);
            }

            if (loadResult.Exception is { } trackException)
            {
                lastException = trackException;
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
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        if (player.Player!.CurrentTrack is null && player.Player.Queue.Count == 0)
        {
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        await player.Player.SkipAsync(cancellationToken: cancellationToken);
        return new MusicCommandResult("Skipped the current track.");
    }

    public async Task<MusicCommandResult> StopAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        await player.Player!.StopAsync(cancellationToken);
        return new MusicCommandResult("Stopped playback and cleared the queue.");
    }

    public async Task<MusicCommandResult> QueueAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player is null || (player.CurrentTrack is null && player.Queue.Count == 0))
        {
            return new MusicCommandResult("The queue is currently empty.", Ephemeral: true);
        }

        return new MusicCommandResult(BuildQueueMessage(player));
    }

    public async Task<MusicCommandResult> NowPlayingAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        var player = await GetExistingPlayerAsync(ctx, cancellationToken);
        if (player?.CurrentTrack is null)
        {
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        var currentTrack = player.CurrentTrack;
        var queueSuffix = player.Queue.Count > 0
            ? $" {player.Queue.Count} more track(s) queued."
            : string.Empty;

        return new MusicCommandResult($"Now playing {FormatTrack(currentTrack)}.{queueSuffix}");
    }

    public async Task<MusicCommandResult> LeaveAsync(ICommandContext ctx, CancellationToken cancellationToken = default)
    {
        var player = await GetControllablePlayerAsync(ctx, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        await player.Player!.StopAsync(cancellationToken);
        await player.Player.DisconnectAsync(cancellationToken);
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
}
