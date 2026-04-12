using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicDashboardControlServiceTests
{
    private static MusicDashboardControlService CreateService(
        ISharedVoiceChannelResolver? resolver = null,
        IAudioService? audioService = null,
        IPlayerManager? playerManager = null,
        PlaybackService? playbackService = null,
        IMusicInactivityTracker? musicInactivityTracker = null)
    {
        resolver ??= Substitute.For<ISharedVoiceChannelResolver>();
        audioService ??= Substitute.For<IAudioService>();
        playerManager ??= Substitute.For<IPlayerManager>();

        audioService.Players.Returns(playerManager);

        playbackService ??= new PlaybackService(
            audioService,
            new RadioTrackMetadataStore(),
            NullLogger<WasabiQueuedLavalinkPlayer>.Instance,
            Substitute.For<IMusicPlaybackStatsRecorder>());

        return new MusicDashboardControlService(
            resolver,
            playbackService,
            new MusicQueueMutationCoordinator(),
            musicInactivityTracker);
    }

    [Test]
    public async Task TogglePauseAsync_WhenUserNotSharingVoiceChannel_ReturnsFriendlyError()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789).Returns((SharedVoiceChannel?)null);
        var service = CreateService(resolver: resolver);

        var result = await service.TogglePauseAsync(123456789);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("You need to be in the same voice channel as the bot.");
    }

    [Test]
    public async Task JoinUserChannelAsync_WhenBotAlreadyConnectedInGuild_ReturnsFriendlyError()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveUserVoiceChannel(123456789)
            .Returns(new UserVoiceChannel(42, "Wasabi HQ", 99, "music-room", BotIsConnectedInGuild: true, BotSharesChannel: false));

        var service = CreateService(resolver: resolver);

        var result = await service.JoinUserChannelAsync(123456789);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("I'm already connected to another voice channel in this server.");
    }

    [Test]
    public async Task TogglePauseAsync_WhenPlayerIsPlaying_PausesPlayback()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.State.Returns(PlayerState.Playing);
        player.CurrentTrack.Returns(new Lavalink4NET.Tracks.LavalinkTrack { Identifier = "current-song", Title = "Current Song", Author = "Artist" });

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var inactivityTracker = Substitute.For<IMusicInactivityTracker>();
        var service = CreateService(resolver: resolver, playerManager: playerManager, musicInactivityTracker: inactivityTracker);

        var result = await service.TogglePauseAsync(123456789);

        await player.Received(1).PauseAsync(Arg.Any<CancellationToken>());
        inactivityTracker.Received(1).SchedulePausedDisconnect(42);
        await Assert.That(result.Message).IsEqualTo("Paused playback.");
    }

    [Test]
    public async Task TogglePauseAsync_WhenPlayerIsPaused_ResumesPlaybackAndCancelsTimeout()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.State.Returns(PlayerState.Paused);
        player.CurrentTrack.Returns(new Lavalink4NET.Tracks.LavalinkTrack { Identifier = "current-song", Title = "Current Song", Author = "Artist" });

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var inactivityTracker = Substitute.For<IMusicInactivityTracker>();
        var service = CreateService(resolver: resolver, playerManager: playerManager, musicInactivityTracker: inactivityTracker);

        var result = await service.TogglePauseAsync(123456789);

        await player.Received(1).ResumeAsync(Arg.Any<CancellationToken>());
        inactivityTracker.Received(1).CancelDisconnect(42);
        await Assert.That(result.Message).IsEqualTo("Resumed playback.");
    }

    [Test]
    public async Task SkipAsync_WhenPlayerHasContent_SkipsTrack()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();
        player.CurrentTrack.Returns(new Lavalink4NET.Tracks.LavalinkTrack { Identifier = "current-song", Title = "Current Song", Author = "Artist" });

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.SkipAsync(123456789);

        await player.Received(1).SkipAsync(1, Arg.Any<CancellationToken>());
        await Assert.That(result.Message).IsEqualTo("Skipped the current track.");
    }

    [Test]
    public async Task StopAsync_WhenPlayerIsConnected_StopsPlaybackAndSchedulesIdleDisconnect()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var inactivityTracker = Substitute.For<IMusicInactivityTracker>();
        var service = CreateService(resolver: resolver, playerManager: playerManager, musicInactivityTracker: inactivityTracker);

        var result = await service.StopAsync(123456789);

        await player.Received(1).StopAsync(Arg.Any<CancellationToken>());
        inactivityTracker.Received(1).ScheduleIdleDisconnect(42);
        await Assert.That(result.Message).IsEqualTo("Stopped playback and cleared the queue.");
    }
}
