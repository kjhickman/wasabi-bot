using WasabiBot.Api.Infrastructure.Database.Entities;

namespace WasabiBot.Api.Features.Music;

public sealed record MusicFavoriteSongMetadata(
    string TrackIdentifier,
    string SourceName,
    string SourceUrl,
    string ArtworkUrl,
    string DurationText);

public sealed record MusicFavoriteRadioMetadata(
    string StationUuid,
    string StreamUrl,
    string HomepageUrl,
    string FaviconUrl,
    string Country,
    string Tags);

public sealed record MusicFavoriteSummary(
    long Id,
    MusicFavoriteKind Kind,
    string Title,
    string Subtitle,
    string SourceName,
    string SourceUrl,
    string ArtworkUrl,
    DateTimeOffset CreatedAt,
    MusicFavoriteSongMetadata? Song,
    MusicFavoriteRadioMetadata? Radio);

public sealed record MusicFavoritesSnapshot(
    IReadOnlyList<MusicFavoriteSummary> Songs,
    IReadOnlyList<MusicFavoriteSummary> RadioStations);
