using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
    private void OnSearchQueryChanged(string value)
    {
        SearchQuery = value;
    }

    private async Task SearchAsync()
    {
        if (IsSearching)
        {
            return;
        }

        var query = SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            HasSearchAttempt = false;
            SearchError = null;
            SearchResults = null;
            return;
        }

        IsSearching = true;
        HasSearchAttempt = true;
        SearchError = null;

        try
        {
            SearchResults = await MusicDashboardSearchService.SearchAsync(query, CancellationToken.None);
            await RefreshFavoritesAsync(CancellationToken.None);
            SearchError = SearchResults.ErrorMessage;
        }
        catch
        {
            SearchError = "Couldn't search music right now. Please try again.";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task QueueSongAsync(MusicDashboardSongSearchResult result)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.QueueTrackAsync(UserId!.Value, result.Track, ct));
    }

    private async Task PlaySongNextAsync(MusicDashboardSongSearchResult result)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.PlayNextTrackAsync(UserId!.Value, result.Track, ct));
    }

    private async Task QueueStationAsync(MusicDashboardRadioSearchResult result)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.QueueStationAsync(UserId!.Value, result.Station, ct));
    }

    private async Task PlayStationNextAsync(MusicDashboardRadioSearchResult result)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.PlayNextStationAsync(UserId!.Value, result.Station, ct));
    }

    private async Task RefreshSearchFavoriteStateAsync(CancellationToken cancellationToken = default)
    {
        if (UserId is null || SearchResults is null)
        {
            return;
        }

        Favorites = await MusicFavoritesService.ListAsync((long)UserId.Value, cancellationToken);
        ApplyFavoriteStateToSearchResults();
    }

    private void ApplyFavoriteStateToSearchResults()
    {
        if (SearchResults is null)
        {
            return;
        }

        var songFavoritesByExternalId = Favorites.Songs
            .Where(favorite => favorite.Song is not null)
            .ToDictionary(
                favorite => !string.IsNullOrWhiteSpace(favorite.SourceUrl)
                    ? favorite.SourceUrl
                    : $"{favorite.SourceName}:{favorite.Song!.TrackIdentifier}",
                favorite => favorite,
                StringComparer.Ordinal);

        var radioFavoritesByStationId = Favorites.RadioStations
            .Where(favorite => favorite.Radio is not null)
            .ToDictionary(favorite => favorite.Radio!.StationUuid, favorite => favorite, StringComparer.Ordinal);

        SearchResults = SearchResults with
        {
            Songs = SearchResults.Songs.Select(song =>
            {
                var externalId = !string.IsNullOrWhiteSpace(song.SourceUrl)
                    ? song.SourceUrl
                    : $"{song.SourceName}:{song.Track.Identifier}";

                return songFavoritesByExternalId.TryGetValue(externalId, out var favorite)
                    ? song with { IsFavorited = true, FavoriteId = favorite.Id }
                    : song with { IsFavorited = false, FavoriteId = null };
            }).ToArray(),
            Stations = SearchResults.Stations.Select(station =>
            {
                return radioFavoritesByStationId.TryGetValue(station.Station.StationUuid, out var favorite)
                    ? station with { IsFavorited = true, FavoriteId = favorite.Id }
                    : station with { IsFavorited = false, FavoriteId = null };
            }).ToArray(),
        };
    }
}
