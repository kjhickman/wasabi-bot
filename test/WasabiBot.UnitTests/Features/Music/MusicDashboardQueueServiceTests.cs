using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicDashboardQueueServiceTests
{
    private static MusicDashboardQueueService CreateService(
        ISharedVoiceChannelResolver? resolver = null,
        IAudioService? audioService = null,
        IPlayerManager? playerManager = null)
    {
        resolver ??= Substitute.For<ISharedVoiceChannelResolver>();
        audioService ??= Substitute.For<IAudioService>();
        playerManager ??= Substitute.For<IPlayerManager>();

        audioService.Players.Returns(playerManager);

        return new MusicDashboardQueueService(
            resolver,
            new PlaybackService(audioService, new RadioTrackMetadataStore(), NullLogger<WasabiQueuedLavalinkPlayer>.Instance, Substitute.For<IMusicPlaybackStatsRecorder>()),
            audioService,
            new RadioTrackMetadataStore(),
            new MusicQueueMutationCoordinator());
    }

    [Test]
    public async Task PlayNextTrackAsync_WhenTrackIsAlreadyPlaying_InsertsAtFrontOfQueue()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.CurrentTrack.Returns(CreateTrack("Current Song", "Artist", TimeSpan.FromMinutes(3)));
        player.Queue.Returns(queue);
        queue.Count.Returns(1);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.PlayNextTrackAsync(123456789, CreateTrack("Next Song", "Artist", TimeSpan.FromMinutes(4)));

        await queue.Received(1).InsertAsync(0, Arg.Any<ITrackQueueItem>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Message).Contains("to play next");
    }

    [Test]
    public async Task RemoveQueueItemAsync_WhenPositionIsValid_RemovesTrack()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        var queueItem = Substitute.For<ITrackQueueItem>();
        queueItem.Track.Returns(CreateTrack("Next Song", "Artist", TimeSpan.FromMinutes(4)));
        player.Queue.Returns(queue);
        queue.Count.Returns(2);
        queue[0].Returns(queueItem);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.RemoveQueueItemAsync(123456789, 1);

        await queue.Received(1).RemoveAtAsync(0, Arg.Any<CancellationToken>());
        await Assert.That(result.Message).Contains("Removed **Next Song** by **Artist** (`04:00`) from the queue.");
    }

    [Test]
    public async Task RemoveQueueItemAsync_WhenPositionIsOutOfRange_ReturnsFriendlyError()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.Queue.Returns(queue);
        queue.Count.Returns(1);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.RemoveQueueItemAsync(123456789, 2);

        await queue.DidNotReceive().RemoveAtAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("That queued track no longer exists.");
    }

    [Test]
    public async Task MoveQueueItemAsync_WhenMoveIsValid_ReinsertsTrackAtRequestedPosition()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        var queueItem = Substitute.For<ITrackQueueItem>();
        queueItem.Track.Returns(CreateTrack("Move Me", "Artist", TimeSpan.FromMinutes(4)));
        player.Queue.Returns(queue);
        queue.Count.Returns(3);
        queue[1].Returns(queueItem);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.MoveQueueItemAsync(123456789, 2, 1);

        await queue.Received(1).RemoveAtAsync(1, Arg.Any<CancellationToken>());
        await queue.Received(1).InsertAsync(0, Arg.Any<ITrackQueueItem>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Message).Contains("Moved **Move Me** by **Artist** (`04:00`) to position 1.");
    }

    [Test]
    public async Task MoveQueueItemAsync_WhenTargetPositionIsInvalid_ReturnsFriendlyError()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        var queueItem = Substitute.For<ITrackQueueItem>();
        queueItem.Track.Returns(CreateTrack("Move Me", "Artist", TimeSpan.FromMinutes(4)));
        player.Queue.Returns(queue);
        queue.Count.Returns(2);
        queue[0].Returns(queueItem);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.MoveQueueItemAsync(123456789, 1, 3);

        await queue.DidNotReceive().RemoveAtAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await queue.DidNotReceive().InsertAsync(Arg.Any<int>(), Arg.Any<ITrackQueueItem>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("That move is no longer valid because the queue changed.");
    }

    [Test]
    public async Task ClearQueueAsync_WhenQueueHasTracks_ClearsQueuedTracks()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.Queue.Returns(queue);
        queue.Count.Returns(2);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.ClearQueueAsync(123456789);

        await queue.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        await Assert.That(result.Message).IsEqualTo("Cleared the queued tracks.");
    }

    [Test]
    public async Task ClearQueueAsync_WhenQueueIsAlreadyEmpty_ReturnsFriendlyError()
    {
        var resolver = CreateResolver();
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.Queue.Returns(queue);
        queue.Count.Returns(0);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.ClearQueueAsync(123456789);

        await queue.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("The queue is already empty.");
    }

    [Test]
    public async Task ClearQueueAsync_WhenUserIsNotSharingVoiceChannel_ReturnsFriendlyError()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789).Returns((SharedVoiceChannel?)null);
        var service = CreateService(resolver: resolver);

        var result = await service.ClearQueueAsync(123456789);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("You need to be in the same voice channel as the bot.");
    }

    private static ISharedVoiceChannelResolver CreateResolver()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));
        return resolver;
    }

    private static LavalinkTrack CreateTrack(string title, string author, TimeSpan duration)
    {
        return new LavalinkTrack
        {
            Identifier = title.ToLowerInvariant().Replace(' ', '-'),
            Title = title,
            Author = author,
            Duration = duration,
            IsSeekable = true,
            SourceName = "scsearch"
        };
    }
}
