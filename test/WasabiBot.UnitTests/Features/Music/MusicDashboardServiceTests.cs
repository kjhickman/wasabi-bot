using System.Collections.Immutable;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicDashboardServiceTests
{
    private static MusicDashboardService CreateService(
        ISharedVoiceChannelResolver? resolver = null,
        IAudioService? audioService = null,
        IPlayerManager? playerManager = null)
    {
        resolver ??= Substitute.For<ISharedVoiceChannelResolver>();
        audioService ??= Substitute.For<IAudioService>();
        playerManager ??= Substitute.For<IPlayerManager>();

        audioService.Players.Returns(playerManager);

        return new MusicDashboardService(
            resolver,
            new PlaybackService(audioService, new RadioTrackMetadataStore()));
    }

    [Test]
    public async Task GetActiveSessionAsync_WhenUserIsNotSharingVoiceWithBot_ReturnsNull()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789).Returns((SharedVoiceChannel?)null);
        var service = CreateService(resolver: resolver);

        var result = await service.GetActiveSessionAsync(123456789);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetActiveSessionAsync_WhenNoPlayerExists_ReturnsSharedChannelWithEmptySnapshot()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(null));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.GetActiveSessionAsync(123456789);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Channel.GuildName).IsEqualTo("Wasabi HQ");
        await Assert.That(result.Channel.VoiceChannelName).IsEqualTo("music-room");
        await Assert.That(result.NowPlaying).IsNull();
        await Assert.That(result.Queue).IsEmpty();
    }

    [Test]
    public async Task GetActiveSessionAsync_WhenPlayerHasTracks_ReturnsStructuredSnapshot()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        var nextTrackItem = Substitute.For<ITrackQueueItem>();

        player.CurrentTrack.Returns(CreateTrack("Current Song", "Current Artist", TimeSpan.FromMinutes(3)));
        player.Queue.Returns(queue);
        queue.Count.Returns(1);
        queue[0].Returns(nextTrackItem);
        nextTrackItem.Track.Returns(CreateTrack("Next Song", "Next Artist", TimeSpan.FromSeconds(95)));

        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = CreateService(resolver: resolver, playerManager: playerManager);

        var result = await service.GetActiveSessionAsync(123456789);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.NowPlaying).IsNotNull();
        await Assert.That(result.NowPlaying!.Title).IsEqualTo("Current Song");
        await Assert.That(result.NowPlaying.Author).IsEqualTo("Current Artist");
        await Assert.That(result.NowPlaying.DurationText).IsEqualTo("03:00");
        await Assert.That(result.Queue).Count().IsEqualTo(1);
        await Assert.That(result.Queue[0].Position).IsEqualTo(1);
        await Assert.That(result.Queue[0].Track.Title).IsEqualTo("Next Song");
        await Assert.That(result.Queue[0].Track.DurationText).IsEqualTo("01:35");
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
