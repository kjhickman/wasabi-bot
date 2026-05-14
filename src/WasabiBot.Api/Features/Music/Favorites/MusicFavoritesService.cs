using System.Text.Json;
using Dapper;
using Lavalink4NET.Tracks;
using Npgsql;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Core.Serialization;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicFavoritesService(
    NpgsqlDataSource dataSource,
    PlaybackService playbackService) : IMusicFavoritesService
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly PlaybackService _playbackService = playbackService;

    public async Task<MusicFavoritesSnapshot> ListAsync(long discordUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "DiscordUserId", "Kind", "ExternalId", "Title", "ArtistOrSubtitle", "SourceName", "SourceUrl", "ArtworkUrl", "MetadataJson", "CreatedAt"
            FROM "MusicFavorites"
            WHERE "DiscordUserId" = @DiscordUserId
            ORDER BY "CreatedAt" DESC
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var favorites = (await connection.QueryAsync<MusicFavoriteRow>(new CommandDefinition(
            sql, new { DiscordUserId = discordUserId }, cancellationToken: cancellationToken)))
            .Select(row => row.ToEntity())
            .ToArray();

        var songs = favorites
            .Where(favorite => favorite.Kind == MusicFavoriteKind.Song)
            .Select(MapFavorite)
            .ToArray();

        var radioStations = favorites
            .Where(favorite => favorite.Kind == MusicFavoriteKind.Radio)
            .Select(MapFavorite)
            .ToArray();

        return new MusicFavoritesSnapshot(songs, radioStations);
    }

    public async Task<MusicCommandResult> AddSongAsync(long discordUserId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        var snapshot = _playbackService.CreateTrackSnapshot(track);
        if (snapshot is null)
        {
            return new MusicCommandResult("Couldn't save that song as a favorite.", Ephemeral: true);
        }

        var externalId = !string.IsNullOrEmpty(snapshot.SourceUrl)
            ? snapshot.SourceUrl
            : $"{snapshot.SourceName}:{track.Identifier}";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await FavoriteExistsAsync(connection, discordUserId, MusicFavoriteKind.Song, externalId, cancellationToken);

        if (exists)
        {
            return new MusicCommandResult("That song is already in your favorites.", Ephemeral: true);
        }

        const string sql = """
            INSERT INTO "MusicFavorites" ("DiscordUserId", "Kind", "ExternalId", "Title", "ArtistOrSubtitle", "SourceName", "SourceUrl", "ArtworkUrl", "MetadataJson", "CreatedAt")
            VALUES (@DiscordUserId, @Kind, @ExternalId, @Title, @ArtistOrSubtitle, @SourceName, @SourceUrl, @ArtworkUrl, CAST(@MetadataJson AS jsonb), @CreatedAt)
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            DiscordUserId = discordUserId,
            Kind = nameof(MusicFavoriteKind.Song),
            ExternalId = externalId,
            Title = snapshot.Title,
            ArtistOrSubtitle = snapshot.Author,
            SourceName = snapshot.SourceName ?? string.Empty,
            SourceUrl = snapshot.SourceUrl ?? string.Empty,
            ArtworkUrl = snapshot.ArtworkUrl ?? string.Empty,
            MetadataJson = JsonSerializer.Serialize(new MusicFavoriteSongMetadata(
                track.Identifier,
                snapshot.SourceName ?? string.Empty,
                snapshot.SourceUrl ?? string.Empty,
                snapshot.ArtworkUrl ?? string.Empty,
                snapshot.DurationText), JsonContext.Default.MusicFavoriteSongMetadata),
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken: cancellationToken));
        return new MusicCommandResult($"Saved **{snapshot.Title}** to your song favorites.");
    }

    public async Task<MusicCommandResult> AddRadioAsync(long discordUserId, RadioBrowserStation station, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await FavoriteExistsAsync(connection, discordUserId, MusicFavoriteKind.Radio, station.StationUuid, cancellationToken);

        if (exists)
        {
            return new MusicCommandResult("That radio station is already in your favorites.", Ephemeral: true);
        }

        const string sql = """
            INSERT INTO "MusicFavorites" ("DiscordUserId", "Kind", "ExternalId", "Title", "ArtistOrSubtitle", "SourceName", "SourceUrl", "ArtworkUrl", "MetadataJson", "CreatedAt")
            VALUES (@DiscordUserId, @Kind, @ExternalId, @Title, @ArtistOrSubtitle, @SourceName, @SourceUrl, @ArtworkUrl, CAST(@MetadataJson AS jsonb), @CreatedAt)
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            DiscordUserId = discordUserId,
            Kind = nameof(MusicFavoriteKind.Radio),
            ExternalId = station.StationUuid,
            Title = station.Name,
            ArtistOrSubtitle = station.Country,
            SourceName = "Radio Browser",
            SourceUrl = station.Homepage,
            ArtworkUrl = station.Favicon,
            MetadataJson = JsonSerializer.Serialize(new MusicFavoriteRadioMetadata(
                station.StationUuid,
                station.UrlResolved,
                station.Homepage,
                station.Favicon,
                station.Country,
                station.Tags), JsonContext.Default.MusicFavoriteRadioMetadata),
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken: cancellationToken));
        return new MusicCommandResult($"Saved **{station.Name}** to your radio favorites.");
    }

    public async Task<MusicCommandResult> RemoveAsync(long discordUserId, long favoriteId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string selectSql = """
            SELECT "Id", "DiscordUserId", "Kind", "ExternalId", "Title", "ArtistOrSubtitle", "SourceName", "SourceUrl", "ArtworkUrl", "MetadataJson", "CreatedAt"
            FROM "MusicFavorites"
            WHERE "Id" = @FavoriteId AND "DiscordUserId" = @DiscordUserId
            """;
        var favorite = (await connection.QueryFirstOrDefaultAsync<MusicFavoriteRow>(new CommandDefinition(
            selectSql, new { FavoriteId = favoriteId, DiscordUserId = discordUserId }, cancellationToken: cancellationToken)))?.ToEntity();

        if (favorite is null)
        {
            return new MusicCommandResult("That favorite no longer exists.", Ephemeral: true);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM \"MusicFavorites\" WHERE \"Id\" = @FavoriteId AND \"DiscordUserId\" = @DiscordUserId",
            new { FavoriteId = favoriteId, DiscordUserId = discordUserId }, cancellationToken: cancellationToken));
        return new MusicCommandResult($"Removed **{favorite.Title}** from your favorites.");
    }

    private static async Task<bool> FavoriteExistsAsync(NpgsqlConnection connection, long discordUserId, MusicFavoriteKind kind, string externalId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM "MusicFavorites"
                WHERE "DiscordUserId" = @DiscordUserId AND "Kind" = @Kind AND "ExternalId" = @ExternalId)
            """;

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { DiscordUserId = discordUserId, Kind = kind.ToString(), ExternalId = externalId }, cancellationToken: cancellationToken));
    }

    private static MusicFavoriteSummary MapFavorite(MusicFavoriteEntity favorite)
    {
        return favorite.Kind switch
        {
            MusicFavoriteKind.Song => new MusicFavoriteSummary(
                favorite.Id,
                favorite.Kind,
                favorite.Title,
                favorite.ArtistOrSubtitle,
                favorite.SourceName,
                favorite.SourceUrl,
                favorite.ArtworkUrl,
                favorite.CreatedAt,
                JsonSerializer.Deserialize(favorite.MetadataJson, JsonContext.Default.MusicFavoriteSongMetadata),
                null),
            _ => new MusicFavoriteSummary(
                favorite.Id,
                favorite.Kind,
                favorite.Title,
                favorite.ArtistOrSubtitle,
                favorite.SourceName,
                favorite.SourceUrl,
                favorite.ArtworkUrl,
                favorite.CreatedAt,
                null,
                JsonSerializer.Deserialize(favorite.MetadataJson, JsonContext.Default.MusicFavoriteRadioMetadata))
        };
    }

    private sealed class MusicFavoriteRow
    {
        public long Id { get; set; }
        public long DiscordUserId { get; set; }
        public required string Kind { get; set; }
        public required string ExternalId { get; set; }
        public required string Title { get; set; }
        public string ArtistOrSubtitle { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string ArtworkUrl { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }

        public MusicFavoriteEntity ToEntity() => new()
        {
            Id = Id,
            DiscordUserId = DiscordUserId,
            Kind = Enum.Parse<MusicFavoriteKind>(Kind),
            ExternalId = ExternalId,
            Title = Title,
            ArtistOrSubtitle = ArtistOrSubtitle,
            SourceName = SourceName,
            SourceUrl = SourceUrl,
            ArtworkUrl = ArtworkUrl,
            MetadataJson = MetadataJson,
            CreatedAt = new DateTimeOffset(CreatedAt.ToUniversalTime()),
        };
    }
}
