using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task PlayAsync_WhenInputIsNotUrl_ReturnsUrlOnlyMessage()
    {
        var service = CreateService();
        var context = new FakeCommandContext();

        var result = await service.PlayAsync(context, "never gonna give you up");

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message)
            .IsEqualTo("For now, `/play` only supports direct track or playlist URLs.");
    }

    [Test]
    public async Task QueueAsync_WhenNoPlayerExists_ReturnsEmptyQueueMessage()
    {
        var audioService = Substitute.For<IAudioService>();
        var playerManager = Substitute.For<IPlayerManager>();
        playerManager.GetPlayerAsync<QueuedLavalinkPlayer>(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<QueuedLavalinkPlayer?>(null));

        var service = CreateService(audioService, playerManager);
        var context = new FakeCommandContext();

        var result = await service.QueueAsync(context);

        await Assert.That(result.Ephemeral).IsTrue();
        await Assert.That(result.Message).IsEqualTo("The queue is currently empty.");
    }
}
