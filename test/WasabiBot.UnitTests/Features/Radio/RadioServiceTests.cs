using Lavalink4NET.Tracks;
using NSubstitute;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.UnitTests.Features.Radio;

public class RadioServiceTests
{
    [Test]
    public async Task RankStations_PrefersExactStationNameAndPlayableStreams()
    {
        var stations = new[]
        {
            new RadioBrowserStation
            {
                Name = "Jazz FM",
                UrlResolved = "https://stream.example/jazzfm",
                Country = "United States",
                Tags = "jazz,smooth",
                Votes = 10,
                ClickCount = 50,
                Bitrate = 128,
                LastCheckOk = 1,
            },
            new RadioBrowserStation
            {
                Name = "Smooth Jazz Lounge",
                UrlResolved = "https://stream.example/lounge",
                Country = "Canada",
                Tags = "jazz,ambient",
                Votes = 200,
                ClickCount = 500,
                Bitrate = 320,
                LastCheckOk = 1,
            },
            new RadioBrowserStation
            {
                Name = "Jazz FM Broken",
                UrlResolved = "",
                Country = "United States",
                Tags = "jazz",
                Votes = 500,
                ClickCount = 1000,
                Bitrate = 320,
                LastCheckOk = 1,
            },
        };

        var ranked = RadioService.RankStations("Jazz FM", stations);

        await Assert.That(ranked).Count().IsEqualTo(2);
        await Assert.That(ranked[0].Name).IsEqualTo("Jazz FM");
        await Assert.That(ranked.All(x => !string.IsNullOrWhiteSpace(x.UrlResolved))).IsTrue();
    }

    [Test]
    public async Task FormatTrack_WhenRadioMetadataExists_UsesStationNameOnly()
    {
        var metadataStore = new RadioTrackMetadataStore();
        var playbackService = new PlaybackService(Substitute.For<Lavalink4NET.IAudioService>(), metadataStore);
        var track = new LavalinkTrack
        {
            Title = "http://localhost/",
            Author = "FastCast4u.com AutoDJ",
            Identifier = "station-1",
            Uri = new Uri("https://stream.example/jazz"),
            Duration = TimeSpan.Zero,
            IsLiveStream = true,
            IsSeekable = false,
            SourceName = "http",
        };

        metadataStore.Set(track, "Jazz FM");

        var formatted = playbackService.FormatTrack(track);

        await Assert.That(formatted).IsEqualTo("**Jazz FM** (`LIVE`)");
    }
}
