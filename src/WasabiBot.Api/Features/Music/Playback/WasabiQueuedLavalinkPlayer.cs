using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;

namespace WasabiBot.Api.Features.Music;

internal sealed class WasabiQueuedLavalinkPlayer(
    IPlayerProperties<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> properties,
    ILogger<WasabiQueuedLavalinkPlayer> logger,
    IMusicPlaybackStatsRecorder musicPlaybackStatsRecorder,
    IMusicInactivityTracker musicInactivityTracker) : QueuedLavalinkPlayer(properties)
{
    private readonly ILogger<WasabiQueuedLavalinkPlayer> _logger = logger;
    private readonly IMusicPlaybackStatsRecorder _musicPlaybackStatsRecorder = musicPlaybackStatsRecorder;
    private readonly IMusicInactivityTracker _musicInactivityTracker = musicInactivityTracker;

    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        _musicInactivityTracker.CancelDisconnect(GuildId);

        _logger.LogInformation(
            "Music track started: {Title} by {Author} ({Duration}) in guild {GuildId}",
            track.Track?.Title,
            track.Track?.Author,
            track.Track?.IsLiveStream == true ? "LIVE" : PlaybackService.FormatDuration(track.Track?.Duration ?? TimeSpan.Zero),
            GuildId);

        if (track.Track is not null)
        {
            await _musicPlaybackStatsRecorder.RecordTrackStartedAsync(GuildId, track.Track, cancellationToken);
        }

        await base.NotifyTrackStartedAsync(track, cancellationToken);
    }

    protected override ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        if (Queue.Count == 0)
        {
            _musicInactivityTracker.ScheduleIdleDisconnect(GuildId);
        }

        _logger.LogWarning(
            "Music track ended: {Title} by {Author}. Reason: {EndReason}. QueueCount: {QueueCount}. Guild: {GuildId}",
            track.Track?.Title,
            track.Track?.Author,
            endReason,
            Queue.Count,
            GuildId);

        return base.NotifyTrackEndedAsync(track, endReason, cancellationToken);
    }

    protected override ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Music track exception for {Title} by {Author}: {Message}. Severity: {Severity}. Cause: {Cause}",
            track.Track?.Title,
            track.Track?.Author,
            exception.Message,
            exception.Severity,
            exception.Cause);

        return base.NotifyTrackExceptionAsync(track, exception, cancellationToken);
    }

    protected override ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Music track stuck: {Title} by {Author}. Threshold: {Threshold}. Guild: {GuildId}",
            track.Track?.Title,
            track.Track?.Author,
            threshold,
            GuildId);

        return base.NotifyTrackStuckAsync(track, threshold, cancellationToken);
    }
}
