using Dapper;
using Npgsql;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicGuildStatsService(NpgsqlDataSource dataSource) : IMusicGuildStatsService
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<IReadOnlyList<GuildTopTrackSummary>> GetTopTracksAsync(ulong guildId, int limit = 10, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Title",
                "Artist",
                "SourceName",
                "SourceUrl",
                "ArtworkUrl",
                "PlayCount",
                "LastPlayedAt"
            FROM "GuildTrackPlays"
            WHERE "GuildId" = @GuildId
            ORDER BY "PlayCount" DESC, "LastPlayedAt" DESC
            LIMIT @Limit
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var tracks = await connection.QueryAsync<GuildTopTrackSummary>(
            new CommandDefinition(sql, new { GuildId = (long)guildId, Limit = limit }, cancellationToken: cancellationToken));

        return tracks.AsList();
    }
}
