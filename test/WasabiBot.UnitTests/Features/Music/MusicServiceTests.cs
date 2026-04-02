using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using NSubstitute;
using WasabiBot.Api.Features.Music;
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

        return new MusicService(audioService, NullLogger<MusicService>.Instance);
    }

    [Test]
    public async Task PlayAsync_WhenUsedOutsideGuild_ReturnsGuildOnlyMessage()
    {
        var service = CreateService();
        var context = new FakeCommandContext(guildId: null, userVoiceChannelId: null);

        var result = await service.PlayAsync(context, "https://example.com/song.mp3");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message)
            .IsEqualTo("Music commands can only be used in a server voice channel.");
    }

    [Test]
    public async Task PlayAsync_WhenSearchHasNoMatches_ReturnsSearchMessage()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.YouTube), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateEmpty()));
        trackManager.LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateEmpty()));

        var service = CreateService(trackManager: trackManager);
        var context = new FakeCommandContext();

        var result = await service.PlayAsync(context, "never gonna give you up");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message)
            .IsEqualTo("I couldn't find anything playable for that search.");
    }

    [Test]
    public async Task PlayAsync_WhenYoutubeSearchHasNoMatches_FallsBackToSoundCloud()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.YouTube), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateEmpty()));
        trackManager.LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateTrack(CreateTrack("Found Song", "Artist", TimeSpan.FromMinutes(2)))));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<QueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager, trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(userVoiceChannelId: null), "never gonna give you up");

        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
        await trackManager.Received(1).LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.YouTube), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>());
        await trackManager.Received(1).LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PlayAsync_WhenYoutubeSearchReturnsTrack_DoesNotFallbackToSoundCloud()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.YouTube), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateSearch(ImmutableArray.Create(CreateTrack("Found Song", "Artist", TimeSpan.FromMinutes(2))))));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<QueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager, trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(userVoiceChannelId: null), "never gonna give you up");

        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
        await trackManager.Received(1).LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.YouTube), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>());
        await trackManager.DidNotReceive().LoadTracksAsync("never gonna give you up", Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task QueueAsync_WhenNoPlayerExists_ReturnsEmptyQueueMessage()
    {
        var audioService = Substitute.For<IAudioService>();
        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(null));

        var service = CreateService(audioService, playerManager);
        var context = new FakeCommandContext();

        var result = await service.QueueAsync(context);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("The queue is currently empty.");
    }

    [Test]
    public async Task SkipAsync_WhenUserNotInVoiceChannel_ReturnsFriendlyError()
    {
        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<QueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager);
        var context = new FakeCommandContext(userVoiceChannelId: null);

        var result = await service.SkipAsync(context);

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

    [Test]
    public async Task PlayAsync_WhenUserNotInVoiceChannel_ReturnsFriendlyError()
    {
        var trackManager = Substitute.For<ITrackManager>();
        trackManager.LoadTracksAsync("https://example.com/song.mp3", Arg.Any<TrackLoadOptions>(), Arg.Any<LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateTrack(CreateTrack("Found Song", "Artist", TimeSpan.FromMinutes(2)))));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager
            .RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>>(),
                Arg.Any<Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(PlayerResult<QueuedLavalinkPlayer>.UserNotInVoiceChannel));

        var service = CreateService(playerManager: playerManager, trackManager: trackManager);

        var result = await service.PlayAsync(new FakeCommandContext(), "https://example.com/song.mp3");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("You need to join a voice channel first.");
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
