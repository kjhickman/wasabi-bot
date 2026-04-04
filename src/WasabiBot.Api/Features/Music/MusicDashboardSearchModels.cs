using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

internal sealed record MusicDashboardSongSearchResult(
    string Title,
    string Author,
    string DurationText,
    string? ArtworkUrl,
    string? SourceUrl,
    string? SourceName,
    LavalinkTrack Track);

internal sealed record MusicDashboardRadioSearchResult(
    string Name,
    string Country,
    string Tags,
    string? ArtworkUrl,
    string? HomepageUrl,
    RadioBrowserStation Station);

internal sealed record MusicDashboardSearchResults(
    IReadOnlyList<MusicDashboardSongSearchResult> Songs,
    IReadOnlyList<MusicDashboardRadioSearchResult> Stations,
    string? ErrorMessage);
