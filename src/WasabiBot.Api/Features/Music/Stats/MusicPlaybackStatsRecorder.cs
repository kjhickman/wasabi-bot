using Dapper;
using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Infrastructure.Database;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicPlaybackStatsRecorder(
    IDbConnectionFactory connectionFactory,
    RadioTrackMetadataStore radioTrackMetadataStore,
    ILogger<MusicPlaybackStatsRecorder> logger) : IMusicPlaybackStatsRecorder
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;
    private readonly ILogger<MusicPlaybackStatsRecorder> _logger = logger;

    public async Task RecordTrackStartedAsync(ulong guildId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        if (_radioTrackMetadataStore.TryGet(track, out _))
        {
            return;
        }

        var externalId = BuildExternalId(track);
        if (externalId.Length == 0)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            const string sql = """
                INSERT INTO "GuildTrackPlays"
                    ("GuildId", "ExternalId", "Title", "Artist", "SourceName", "SourceUrl", "ArtworkUrl", "PlayCount", "FirstPlayedAt", "LastPlayedAt")
                VALUES
                    (@GuildId, @ExternalId, @Title, @Artist, @SourceName, @SourceUrl, @ArtworkUrl, 1, @Now, @Now)
                ON CONFLICT ("GuildId", "ExternalId") DO UPDATE SET
                    "Title" = EXCLUDED."Title",
                    "Artist" = EXCLUDED."Artist",
                    "SourceName" = EXCLUDED."SourceName",
                    "SourceUrl" = EXCLUDED."SourceUrl",
                    "ArtworkUrl" = EXCLUDED."ArtworkUrl",
                    "PlayCount" = "GuildTrackPlays"."PlayCount" + 1,
                    "LastPlayedAt" = EXCLUDED."LastPlayedAt"
            """;

            using var connection = await _connectionFactory.CreateConnection(cancellationToken);
            await connection.ExecuteAsync(sql, new
            {
                GuildId = (long)guildId,
                ExternalId = externalId,
                Title = track.Title,
                Artist = track.Author,
                SourceName = track.SourceName ?? string.Empty,
                SourceUrl = track.Uri?.ToString() ?? string.Empty,
                ArtworkUrl = track.ArtworkUri?.ToString() ?? string.Empty,
                Now = now,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record music play stats for guild {GuildId} and track {TrackTitle}", guildId, track.Title);
        }
    }

    private static string BuildExternalId(LavalinkTrack track)
    {
        if (track.Uri is not null)
        {
            return track.Uri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(track.SourceName) && !string.IsNullOrWhiteSpace(track.Identifier))
        {
            return $"{track.SourceName}:{track.Identifier}";
        }

        return string.Empty;
    }
}
