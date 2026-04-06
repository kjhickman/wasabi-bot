using System.Collections.Immutable;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicServiceTests
{
    private static MusicService CreateService(
        IAudioService? audioService = null,
        IPlayerManager? playerManager = null,
        ITrackManager? trackManager = null)
    {
        audioService ??= Substitute.For<IAudioService>();
        playerManager ??= Substitute.For<IPlayerManager>();
        trackManager ??= Substitute.For<ITrackManager>();

        audioService.Players.Returns(playerManager);
        audioService.Tracks.Returns(trackManager);

        return new MusicService(
            audioService,
            new PlaybackService(audioService, new RadioTrackMetadataStore(), NullLogger<WasabiQueuedLavalinkPlayer>.Instance, Substitute.For<IMusicPlaybackStatsRecorder>()),
            NullLogger<MusicService>.Instance,
            TracerProvider.Default.GetTracer("music-tests"));
    }

    [Test]
    public async Task PlayAsync_WhenUsedOutsideGuild_ReturnsGuildOnlyMessage()
    {
        var service = CreateService();
        var context = new FakeCommandContext(guildId: null, userVoiceChannelId: null);

        var result = await service.PlayAsync(context, "https://soundcloud.com/test/song");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message)
            .IsEqualTo("Music commands can only be used in a server voice channel.");
    }

    [Test]
    public async Task PlayAsync_WhenSearchHasNoMatches_ReturnsSoundCloudSearchMessage()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync(
                "never gonna give you up",
                Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud),
                Arg.Any<LavalinkApiResolutionScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateEmpty()));

        var service = CreateService(trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(), "never gonna give you up");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message)
            .IsEqualTo("I couldn't find anything playable on SoundCloud for that search.");
    }

    [Test]
    public async Task PlayAsync_WhenSearchFindsTrackWithoutVoiceChannel_ReturnsFriendlyError()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync(
                "never gonna give you up",
                Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud),
                Arg.Any<LavalinkApiResolutionScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateSearch(ImmutableArray.Create(CreateTrack("Found Song", "Artist", TimeSpan.FromMinutes(2))))));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<WasabiQueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager, trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(userVoiceChannelId: null), "never gonna give you up");

        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
        await trackManager.Received(1).LoadTracksAsync(
            "never gonna give you up",
            Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud),
            Arg.Any<LavalinkApiResolutionScope>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PlayAsync_WhenUrlIsProvided_UsesPassthroughLoad()
    {
        const string url = "https://soundcloud.com/test/song";

        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync(
                url,
                Arg.Is<TrackLoadOptions>(x => x.SearchBehavior == StrictSearchBehavior.Passthrough),
                Arg.Any<LavalinkApiResolutionScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateTrack(CreateTrack("Found Song", "Artist", TimeSpan.FromMinutes(2)))));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<WasabiQueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager, trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(), url);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
        await trackManager.Received(1).LoadTracksAsync(
            url,
            Arg.Is<TrackLoadOptions>(x => x.SearchBehavior == StrictSearchBehavior.Passthrough),
            Arg.Any<LavalinkApiResolutionScope>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task QueueAsync_WhenNoPlayerExists_ReturnsEmptyQueueMessage()
    {
        var audioService = Substitute.For<IAudioService>();
        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(null));

        var service = CreateService(audioService, playerManager);

        var result = await service.QueueAsync(new FakeCommandContext());

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("The queue is currently empty.");
    }

    [Test]
    public async Task SkipAsync_WhenUserNotInVoiceChannel_ReturnsFriendlyError()
    {
        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<WasabiQueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<WasabiQueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager);

        var result = await service.SkipAsync(new FakeCommandContext(userVoiceChannelId: null));

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
    }

    [Test]
    public async Task QueueAsync_WhenPlayerHasTracks_ReturnsFormattedQueue()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        var nextTrackItem = Substitute.For<ITrackQueueItem>();

        var currentTrack = CreateTrack("Current Song", "Current Artist", TimeSpan.FromMinutes(3));
        var nextTrack = CreateTrack("Next Song", "Next Artist", TimeSpan.FromMinutes(4));

        player.CurrentTrack.Returns(currentTrack);
        player.Queue.Returns(queue);
        queue.Count.Returns(1);
        queue[0].Returns(nextTrackItem);
        nextTrackItem.Track.Returns(nextTrack);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(playerManager: playerManager);

        var result = await service.QueueAsync(new FakeCommandContext());

        await Assert.That(result.Ephemeral).IsFalse();
        await Assert.That(result.Message).Contains("Now playing: **Current Song** by **Current Artist** (`03:00`)");
        await Assert.That(result.Message).Contains("1. **Next Song** by **Next Artist** (`04:00`)");
    }

    [Test]
    public async Task NowPlayingAsync_WhenQueueHasMoreTracks_IncludesQueuedCount()
    {
        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.CurrentTrack.Returns(CreateTrack("Current Song", "Current Artist", TimeSpan.FromSeconds(95)));
        player.Queue.Returns(queue);
        queue.Count.Returns(2);

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(playerManager: playerManager);

        var result = await service.NowPlayingAsync(new FakeCommandContext());

        await Assert.That(result.Ephemeral).IsFalse();
        await Assert.That(result.Message).Contains("Now playing **Current Song** by **Current Artist** (`01:35`). 2 more track(s) queued.");
    }

    private static LavalinkTrack CreateTrack(string title, string author, TimeSpan duration)
    {
        return new LavalinkTrack
        {
            Title = title,
            Author = author,
            Identifier = title.ToLowerInvariant().Replace(' ', '-'),
            Duration = duration,
            IsLiveStream = false,
            IsSeekable = true,
            SourceName = "http"
        };
    }
}
