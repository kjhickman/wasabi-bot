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
    [Test]
    public async Task PlayNextTrackAsync_WhenTrackIsAlreadyPlaying_InsertsAtFrontOfQueue()
    {
        var resolver = Substitute.For<ISharedVoiceChannelResolver>();
        resolver.ResolveForUser(123456789)
            .Returns(new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"));

        var player = Substitute.For<IQueuedLavalinkPlayer>();
        var queue = Substitute.For<ITrackQueue>();
        player.CurrentTrack.Returns(new LavalinkTrack { Identifier = "current-song", Title = "Current Song", Author = "Artist" });
        player.Queue.Returns(queue);
        queue.Count.Returns(1);

        var audioService = Substitute.For<IAudioService>();
        var playerManager = Substitute.For<IPlayerManager>();
        audioService.Players.Returns(playerManager);
        playerManager.GetPlayerAsync(42, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ILavalinkPlayer?>(player));

        var service = new MusicDashboardQueueService(
            resolver,
            new PlaybackService(audioService, new RadioTrackMetadataStore(), NullLogger<WasabiQueuedLavalinkPlayer>.Instance, Substitute.For<IMusicPlaybackStatsRecorder>()),
            audioService,
            new RadioTrackMetadataStore());

        var result = await service.PlayNextTrackAsync(123456789, new LavalinkTrack
        {
            Identifier = "next-song",
            Title = "Next Song",
            Author = "Artist",
            Duration = TimeSpan.FromMinutes(4),
            IsSeekable = true,
            SourceName = "scsearch"
        });

        await queue.Received(1).InsertAsync(0, Arg.Any<ITrackQueueItem>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Message).Contains("to play next");
    }
}
