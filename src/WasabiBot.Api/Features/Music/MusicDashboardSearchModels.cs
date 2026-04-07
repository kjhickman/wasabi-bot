using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

public sealed record MusicDashboardSongSearchResult(
    string Title,
    string Author,
    string DurationText,
    string? ArtworkUrl,
    string? SourceUrl,
    string? SourceName,
    LavalinkTrack Track);

public sealed record MusicDashboardRadioSearchResult(
    string Name,
    string Country,
    string Tags,
    string? ArtworkUrl,
    string? HomepageUrl,
    RadioBrowserStation Station);

public sealed record MusicDashboardSearchResults(
    IReadOnlyList<MusicDashboardSongSearchResult> Songs,
    IReadOnlyList<MusicDashboardRadioSearchResult> Stations,
    string? ErrorMessage);
