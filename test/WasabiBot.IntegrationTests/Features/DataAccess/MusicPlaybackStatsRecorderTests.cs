using Lavalink4NET.Tracks;
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

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "PlayCount", "Title"
            FROM "GuildTrackPlays"
            LIMIT 1
            """;
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        var playCount = reader.GetInt64(0);
        var title = reader.GetString(1);

        await Assert.That(playCount).IsEqualTo(2);
        await Assert.That(title).IsEqualTo("Creep");
    }
}
