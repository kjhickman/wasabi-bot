using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.TestShared.Infrastructure;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicPlaybackStatsRecorderTests
{
    [Test]
    public async Task RecordTrackStartedAsync_WhenRadioTrack_DoesNotOpenDatabaseConnection()
    {
        var radioTrackMetadataStore = new RadioTrackMetadataStore();
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var recorder = new MusicPlaybackStatsRecorder(
            new TestDbConnectionFactory(dataSource),
            radioTrackMetadataStore,
            NullLogger<MusicPlaybackStatsRecorder>.Instance);

        var track = new LavalinkTrack
        {
            Identifier = "creep",
            Title = "Creep",
            Author = "Radiohead",
            SourceName = "scsearch",
            Uri = new Uri("https://soundcloud.com/radiohead/creep"),
            IsSeekable = true,
            Duration = TimeSpan.FromMinutes(4),
        };

        radioTrackMetadataStore.Set(track, "Radiohead FM");

        await recorder.RecordTrackStartedAsync(42, track);
    }
}
