using Lavalink4NET.Tracks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Persistence;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicFavoritesServiceTests
{
    [Test]
    public async Task AddSongAsync_PersistsFavoriteAndListsIt()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.AddSongAsync(123456789, new LavalinkTrack
        {
            Identifier = "creep",
            Title = "Creep",
            Author = "Radiohead",
            Duration = TimeSpan.FromMinutes(4),
            IsSeekable = true,
            SourceName = "scsearch",
            Uri = new Uri("https://soundcloud.com/radiohead/creep"),
            ArtworkUri = new Uri("https://cdn.example.com/creep.jpg")
        });

        var favorites = await service.ListAsync(123456789);

        await Assert.That(result.Ephemeral).IsFalse();
        await Assert.That(favorites.Songs).Count().IsEqualTo(1);
        await Assert.That(favorites.Songs[0].Title).IsEqualTo("Creep");
    }

    [Test]
    public async Task AddRadioAsync_PersistsFavoriteAndAllowsRemoval()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.AddRadioAsync(123456789, new RadioBrowserStation
        {
            StationUuid = "station-1",
            Name = "Radiohead FM",
            UrlResolved = "https://stream.example.com/radiohead",
            Homepage = "https://example.com/radiohead",
            Favicon = "https://example.com/radiohead.png",
            Country = "UK",
            Tags = "alternative, rock",
            LastCheckOk = 1,
        });

        var saved = await service.ListAsync(123456789);
        var removeResult = await service.RemoveAsync(123456789, saved.RadioStations[0].Id);
        var afterRemove = await service.ListAsync(123456789);

        await Assert.That(removeResult.Ephemeral).IsFalse();
        await Assert.That(afterRemove.RadioStations).IsEmpty();
    }

    [Test]
    public async Task AddSongAsync_WhenDuplicate_ReturnsFriendlyMessage()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var track = new LavalinkTrack
        {
            Identifier = "creep",
            Title = "Creep",
            Author = "Radiohead",
            Duration = TimeSpan.FromMinutes(4),
            IsSeekable = true,
            SourceName = "scsearch",
            Uri = new Uri("https://soundcloud.com/radiohead/creep")
        };

        await service.AddSongAsync(123456789, track);
        var secondResult = await service.AddSongAsync(123456789, track);

        await Assert.That(secondResult.Ephemeral).IsTrue();
        await Assert.That(secondResult.Message).Contains("already in your favorites");
    }

    private static MusicFavoritesService CreateService(WasabiBotContext context)
    {
        return new MusicFavoritesService(context, new PlaybackService(Substitute.For<Lavalink4NET.IAudioService>(), new RadioTrackMetadataStore(), NullLogger<WasabiQueuedLavalinkPlayer>.Instance, Substitute.For<IMusicPlaybackStatsRecorder>()));
    }

    private static WasabiBotContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WasabiBotContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WasabiBotContext(options);
    }
}
