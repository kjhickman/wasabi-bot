using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
    private async Task SaveSongFavoriteAsync(MusicDashboardSongSearchResult result)
    {
        if (result.IsFavorited && result.FavoriteId is { } favoriteId)
        {
            await ExecuteActionAsync(async ct =>
            {
                var actionResult = await MusicFavoritesService.RemoveAsync((long)UserId!.Value, favoriteId, ct);
                await RefreshFavoritesAsync(ct);
                await RefreshSearchFavoriteStateAsync(ct);
                return actionResult;
            });

            return;
        }

        await ExecuteActionAsync(async ct =>
        {
            var actionResult = await MusicFavoritesService.AddSongAsync((long)UserId!.Value, result.Track, ct);
            await RefreshFavoritesAsync(ct);
            await RefreshSearchFavoriteStateAsync(ct);
            return actionResult;
        });
    }

    private async Task SaveRadioFavoriteAsync(MusicDashboardRadioSearchResult result)
    {
        if (result.IsFavorited && result.FavoriteId is { } favoriteId)
        {
            await ExecuteActionAsync(async ct =>
            {
                var actionResult = await MusicFavoritesService.RemoveAsync((long)UserId!.Value, favoriteId, ct);
                await RefreshFavoritesAsync(ct);
                await RefreshSearchFavoriteStateAsync(ct);
                return actionResult;
            });

            return;
        }

        await ExecuteActionAsync(async ct =>
        {
            var actionResult = await MusicFavoritesService.AddRadioAsync((long)UserId!.Value, result.Station, ct);
            await RefreshFavoritesAsync(ct);
            await RefreshSearchFavoriteStateAsync(ct);
            return actionResult;
        });
    }

    private async Task RemoveFavoriteAsync(MusicFavoriteSummary favorite)
    {
        await ExecuteActionAsync(async ct =>
        {
            var actionResult = await MusicFavoritesService.RemoveAsync((long)UserId!.Value, favorite.Id, ct);
            await RefreshFavoritesAsync(ct);
            return actionResult;
        });
    }

    private async Task QueueFavoriteAsync(MusicFavoriteSummary favorite)
    {
        await ExecuteActionAsync(ct => QueueFavoriteCoreAsync(favorite, playNext: false, ct));
    }

    private async Task PlayFavoriteNextAsync(MusicFavoriteSummary favorite)
    {
        await ExecuteActionAsync(ct => QueueFavoriteCoreAsync(favorite, playNext: true, ct));
    }

    private async Task RefreshFavoritesAsync(CancellationToken cancellationToken = default)
    {
        if (UserId is null)
        {
            Favorites = new MusicFavoritesSnapshot([], []);
            return;
        }

        IsLoadingFavorites = true;

        try
        {
            Favorites = await MusicFavoritesService.ListAsync((long)UserId.Value, cancellationToken);
            ApplyFavoriteStateToSearchResults();
            ApplyFavoriteStateToMostPlayedTracks();
        }
        finally
        {
            IsLoadingFavorites = false;
        }
    }

    private async Task<MusicCommandResult> QueueFavoriteCoreAsync(MusicFavoriteSummary favorite, bool playNext, CancellationToken cancellationToken)
    {
        if (favorite.Song is not null)
        {
            var track = new Lavalink4NET.Tracks.LavalinkTrack
            {
                Identifier = favorite.Song.TrackIdentifier,
                Title = favorite.Title,
                Author = favorite.Subtitle,
                SourceName = favorite.Song.SourceName,
                Uri = Uri.TryCreate(favorite.Song.SourceUrl, UriKind.Absolute, out var songUri) ? songUri : null,
                ArtworkUri = Uri.TryCreate(favorite.Song.ArtworkUrl, UriKind.Absolute, out var artworkUri) ? artworkUri : null,
                IsSeekable = true,
            };

            return playNext
                ? await MusicDashboardQueueService.PlayNextTrackAsync(UserId!.Value, track, cancellationToken)
                : await MusicDashboardQueueService.QueueTrackAsync(UserId!.Value, track, cancellationToken);
        }

        if (favorite.Radio is not null)
        {
            var station = new Features.Radio.RadioBrowserStation
            {
                StationUuid = favorite.Radio.StationUuid,
                Name = favorite.Title,
                UrlResolved = favorite.Radio.StreamUrl,
                Homepage = favorite.Radio.HomepageUrl,
                Favicon = favorite.Radio.FaviconUrl,
                Country = favorite.Radio.Country,
                Tags = favorite.Radio.Tags,
                LastCheckOk = 1,
            };

            return playNext
                ? await MusicDashboardQueueService.PlayNextStationAsync(UserId!.Value, station, cancellationToken)
                : await MusicDashboardQueueService.QueueStationAsync(UserId!.Value, station, cancellationToken);
        }

        return new MusicCommandResult("That favorite can't be played right now.", Ephemeral: true);
    }
}
