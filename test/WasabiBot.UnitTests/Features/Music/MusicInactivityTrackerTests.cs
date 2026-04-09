using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicInactivityTrackerTests
{
    [Test]
    public async Task ScheduleIdleDisconnect_WhenPlayerStaysIdle_DisconnectsPlayer()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.CurrentTrack.Returns((Lavalink4NET.Tracks.LavalinkTrack?)null);

        var tracker = CreateTracker(player, out _);

        tracker.ScheduleIdleDisconnect(42, TimeSpan.FromMilliseconds(10));
        await Task.Delay(100);

        await player.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await player.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SchedulePausedDisconnect_WhenPlayerIsStillPaused_DisconnectsPlayer()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.State.Returns(PlayerState.Paused);

        var tracker = CreateTracker(player, out _);

        tracker.SchedulePausedDisconnect(42, TimeSpan.FromMilliseconds(10));
        await Task.Delay(100);

        await player.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await player.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SchedulePausedDisconnect_WhenPlayerResumesBeforeTimeout_DoesNotDisconnectPlayer()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.State.Returns(PlayerState.Playing);

        var tracker = CreateTracker(player, out _);

        tracker.SchedulePausedDisconnect(42, TimeSpan.FromMilliseconds(10));
        await Task.Delay(100);

        await player.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CancelDisconnect_WhenCalledBeforeTimeout_DoesNotDisconnectPlayer()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.CurrentTrack.Returns((Lavalink4NET.Tracks.LavalinkTrack?)null);

        var tracker = CreateTracker(player, out _);

        tracker.ScheduleIdleDisconnect(42, TimeSpan.FromMilliseconds(50));
        tracker.CancelDisconnect(42);
        await Task.Delay(100);

        await player.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    private static MusicInactivityTracker CreateTracker(IQueuedLavalinkPlayer player, out IPlayerManager playerManager)
    {
        var audioService = Substitute.For<IAudioService>();
        playerManager = Substitute.For<IPlayerManager>();
        audioService.Players.Returns(playerManager);
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var playbackService = new PlaybackService(
            audioService,
            new RadioTrackMetadataStore(),
            NullLogger<WasabiQueuedLavalinkPlayer>.Instance,
            Substitute.For<IMusicPlaybackStatsRecorder>());

        var services = new ServiceCollection();
        services.AddSingleton(playbackService);
        services.AddSingleton<IMusicQueueMutationCoordinator>(new MusicQueueMutationCoordinator());
        var provider = services.BuildServiceProvider();

        return new MusicInactivityTracker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MusicInactivityOptions()),
            NullLogger<MusicInactivityTracker>.Instance);
    }
}
