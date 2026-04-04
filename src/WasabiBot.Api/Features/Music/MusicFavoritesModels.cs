using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Music;

internal sealed record MusicFavoriteSongMetadata(
    string TrackIdentifier,
    string SourceName,
    string SourceUrl,
    string ArtworkUrl,
    string DurationText);

internal sealed record MusicFavoriteRadioMetadata(
    string StationUuid,
    string StreamUrl,
    string HomepageUrl,
    string FaviconUrl,
    string Country,
    string Tags);

internal sealed record MusicFavoriteSummary(
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

internal sealed record MusicFavoritesSnapshot(
    IReadOnlyList<MusicFavoriteSummary> Songs,
    IReadOnlyList<MusicFavoriteSummary> RadioStations);
