using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardSearchService(
    IAudioService audioService,
    IRadioService radioService,
    PlaybackService playbackService) : IMusicDashboardSearchService
{
    private const int SongResultLimit = 5;
    private const int RadioResultLimit = 5;

    private readonly IAudioService _audioService = audioService;
    private readonly IRadioService _radioService = radioService;
    private readonly PlaybackService _playbackService = playbackService;

    public async Task<MusicDashboardSearchResults> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return new MusicDashboardSearchResults([], [], "Enter a song or station search first.");
        }

        var songsTask = SearchSongsAsync(normalizedQuery, cancellationToken);
        var stationsTask = _radioService.SearchStationsAsync(normalizedQuery, cancellationToken);
        await Task.WhenAll(songsTask, stationsTask);

        var songs = songsTask.Result;
        var stations = stationsTask.Result
            .Take(RadioResultLimit)
            .Select(station => new MusicDashboardRadioSearchResult(
                station.Name,
                station.Country,
                station.Tags,
                NormalizeUrl(station.Favicon),
                NormalizeUrl(station.Homepage),
                station))
            .ToArray();

        var errorMessage = songs.Count == 0 && stations.Length == 0
            ? "No playable SoundCloud songs or radio stations matched that search."
            : null;

        return new MusicDashboardSearchResults(songs, stations, errorMessage);
    }

    private async Task<IReadOnlyList<MusicDashboardSongSearchResult>> SearchSongsAsync(string query, CancellationToken cancellationToken)
    {
        var loadResult = await _audioService.Tracks.LoadTracksAsync(
            query,
            new TrackLoadOptions(SearchMode: TrackSearchMode.SoundCloud, SearchBehavior: StrictSearchBehavior.Explicit),
            cancellationToken: cancellationToken);

        if (!loadResult.HasMatches)
        {
            return [];
        }

        return loadResult.Tracks
            .Take(SongResultLimit)
            .Select(MapSongResult)
            .Where(result => result is not null)
            .Cast<MusicDashboardSongSearchResult>()
            .ToArray();
    }

    private MusicDashboardSongSearchResult? MapSongResult(LavalinkTrack track)
    {
        var snapshot = _playbackService.CreateTrackSnapshot(track);
        if (snapshot is null)
        {
            return null;
        }

        return new MusicDashboardSongSearchResult(
            snapshot.Title,
            snapshot.Author,
            snapshot.DurationText,
            snapshot.ArtworkUrl,
            snapshot.SourceUrl,
            snapshot.SourceName,
            track);
    }

    private static string? NormalizeUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.ToString() : null;
    }
}
