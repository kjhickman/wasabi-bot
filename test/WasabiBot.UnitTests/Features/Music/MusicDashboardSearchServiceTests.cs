using System.Collections.Immutable;
using Lavalink4NET;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicDashboardSearchServiceTests
{
    [Test]
    public async Task SearchAsync_WhenSongsAndStationsExist_ReturnsBothSections()
    {
        var audioService = Substitute.For<IAudioService>();
        var trackManager = Substitute.For<ITrackManager>();
        var radioService = Substitute.For<IRadioService>();
        audioService.Tracks.Returns(trackManager);

        trackManager.LoadTracksAsync(
                "radiohead",
                Arg.Is<TrackLoadOptions>(x => x.SearchMode == TrackSearchMode.SoundCloud),
                Arg.Any<LavalinkApiResolutionScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TrackLoadResult.CreateSearch(ImmutableArray.Create(
                CreateTrack("Creep", "Radiohead", TimeSpan.FromMinutes(3), "https://soundcloud.com/radiohead/creep")))));

        radioService.SearchStationsAsync("radiohead", Arg.Any<CancellationToken>())
            .Returns([
                new RadioBrowserStation
                {
                    StationUuid = "station-1",
                    Name = "Radiohead FM",
                    Country = "UK",
                    Tags = "alternative, rock",
                    UrlResolved = "https://stream.example.com/radiohead",
                    Homepage = "https://example.com/radiohead",
                    Favicon = "https://example.com/radiohead.png",
                    LastCheckOk = 1
                }
            ]);

        var service = new MusicDashboardSearchService(audioService, radioService, new PlaybackService(audioService, new RadioTrackMetadataStore(), NullLogger<WasabiQueuedLavalinkPlayer>.Instance, Substitute.For<IMusicPlaybackStatsRecorder>()));

        var result = await service.SearchAsync("radiohead");

        await Assert.That(result.ErrorMessage).IsNull();
        await Assert.That(result.Songs).Count().IsEqualTo(1);
        await Assert.That(result.Stations).Count().IsEqualTo(1);
        await Assert.That(result.Songs[0].Title).IsEqualTo("Creep");
        await Assert.That(result.Stations[0].Name).IsEqualTo("Radiohead FM");
    }

    private static LavalinkTrack CreateTrack(string title, string author, TimeSpan duration, string sourceUrl)
    {
        return new LavalinkTrack
        {
            Identifier = title.ToLowerInvariant().Replace(' ', '-'),
            Title = title,
            Author = author,
            Duration = duration,
            IsSeekable = true,
            IsLiveStream = false,
            SourceName = "scsearch",
            Uri = new Uri(sourceUrl)
        };
    }
}
