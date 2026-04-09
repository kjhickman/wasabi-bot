using System.Collections.Concurrent;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicInactivityTracker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<MusicInactivityOptions> options,
    ILogger<MusicInactivityTracker> logger) : IMusicInactivityTracker
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<MusicInactivityTracker> _logger = logger;
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(options.Value.IdleTimeoutMinutes);
    private readonly TimeSpan _pausedTimeout = TimeSpan.FromMinutes(options.Value.PausedTimeoutMinutes);
    private readonly ConcurrentDictionary<ulong, ScheduledDisconnect> _scheduledDisconnects = new();

    public void ScheduleIdleDisconnect(ulong guildId)
    {
        ScheduleDisconnect(guildId, DisconnectReason.Idle, _idleTimeout);
    }

    internal void ScheduleIdleDisconnect(ulong guildId, TimeSpan timeout)
    {
        ScheduleDisconnect(guildId, DisconnectReason.Idle, timeout);
    }

    public void SchedulePausedDisconnect(ulong guildId)
    {
        ScheduleDisconnect(guildId, DisconnectReason.Paused, _pausedTimeout);
    }

    internal void SchedulePausedDisconnect(ulong guildId, TimeSpan timeout)
    {
        ScheduleDisconnect(guildId, DisconnectReason.Paused, timeout);
    }

    public void CancelDisconnect(ulong guildId)
    {
        if (_scheduledDisconnects.TryRemove(guildId, out var scheduledDisconnect))
        {
            scheduledDisconnect.CancellationTokenSource.Cancel();
            scheduledDisconnect.CancellationTokenSource.Dispose();
        }
    }

    private void ScheduleDisconnect(ulong guildId, DisconnectReason reason, TimeSpan timeout)
    {
        CancelDisconnect(guildId);

        var cancellationTokenSource = new CancellationTokenSource();
        var scheduledDisconnect = new ScheduledDisconnect(reason, cancellationTokenSource);
        _scheduledDisconnects[guildId] = scheduledDisconnect;

        _ = RunDisconnectTimerAsync(guildId, scheduledDisconnect, timeout);
    }

    private async Task RunDisconnectTimerAsync(ulong guildId, ScheduledDisconnect scheduledDisconnect, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, scheduledDisconnect.CancellationTokenSource.Token);
            await DisconnectIfStillInactiveAsync(guildId, scheduledDisconnect.Reason, scheduledDisconnect.CancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process music inactivity timeout for guild {GuildId}", guildId);
        }
        finally
        {
            if (_scheduledDisconnects.TryGetValue(guildId, out var current) && ReferenceEquals(current, scheduledDisconnect))
            {
                _scheduledDisconnects.TryRemove(guildId, out _);
            }

            scheduledDisconnect.CancellationTokenSource.Dispose();
        }
    }

    private async Task DisconnectIfStillInactiveAsync(ulong guildId, DisconnectReason reason, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var playbackService = scope.ServiceProvider.GetRequiredService<PlaybackService>();
        var queueMutationCoordinator = scope.ServiceProvider.GetRequiredService<IMusicQueueMutationCoordinator>();

        await queueMutationCoordinator.ExecuteAsync(guildId, async ct =>
        {
            var player = await playbackService.GetExistingPlayerAsync(guildId, ct);
            if (!ShouldDisconnect(player, reason))
            {
                return false;
            }

            await player!.StopAsync(ct);
            await player.DisconnectAsync(ct);
            _logger.LogInformation("Disconnected inactive music session for guild {GuildId}. Reason: {Reason}", guildId, reason);
            return true;
        }, cancellationToken);
    }

    private static bool ShouldDisconnect(IQueuedLavalinkPlayer? player, DisconnectReason reason)
    {
        if (player is null)
        {
            return false;
        }

        return reason switch
        {
            DisconnectReason.Idle => player.CurrentTrack is null,
            DisconnectReason.Paused => player.State == PlayerState.Paused,
            _ => false
        };
    }

    private sealed record ScheduledDisconnect(DisconnectReason Reason, CancellationTokenSource CancellationTokenSource);

    private enum DisconnectReason
    {
        Idle,
        Paused
    }
}
