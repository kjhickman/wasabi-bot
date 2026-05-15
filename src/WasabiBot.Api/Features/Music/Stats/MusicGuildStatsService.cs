using Dapper;
using WasabiBot.Api.Infrastructure.Database;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicGuildStatsService(IDbConnectionFactory connectionFactory) : IMusicGuildStatsService
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

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

        using var connection = await _connectionFactory.CreateConnection(cancellationToken);
        var tracks = await connection.QueryAsync<GuildTopTrackSummaryRow>(sql, new { GuildId = (long)guildId, Limit = limit });

        return tracks.Select(row => row.ToSummary()).ToArray();
    }

    internal sealed class GuildTopTrackSummaryRow
    {
        public required string Title { get; set; }
        public required string Artist { get; set; }
        public required string SourceName { get; set; }
        public required string SourceUrl { get; set; }
        public required string ArtworkUrl { get; set; }
        public long PlayCount { get; set; }
        public DateTime LastPlayedAt { get; set; }

        public GuildTopTrackSummary ToSummary() => new(
            Title,
            Artist,
            SourceName,
            SourceUrl,
            ArtworkUrl,
            PlayCount,
            new DateTimeOffset(LastPlayedAt.ToUniversalTime()));
    }
}
