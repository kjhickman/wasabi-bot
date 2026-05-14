using Lavalink4NET.Tracks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.IntegrationTests.Infrastructure;

namespace WasabiBot.IntegrationTests.Features.DataAccess;

public class MusicPlaybackStatsRecorderTests : IntegrationTestBase
{
    [Test]
    public async Task RecordTrackStartedAsync_PersistsAndIncrementsGuildPlayCount()
    {
        await using var dataSource = NpgsqlDataSource.Create(Fixture.ConnectionString);
        var recorder = new MusicPlaybackStatsRecorder(
            dataSource,
            new RadioTrackMetadataStore(),
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

        await recorder.RecordTrackStartedAsync(42, track);
        await recorder.RecordTrackStartedAsync(42, track);

        await using var context = CreateContext();
        var play = context.GuildTrackPlays.Single();
        await Assert.That(play.PlayCount).IsEqualTo(2);
        await Assert.That(play.Title).IsEqualTo("Creep");
    }
}
