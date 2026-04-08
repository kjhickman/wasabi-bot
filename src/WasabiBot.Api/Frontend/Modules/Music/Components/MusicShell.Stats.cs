using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
    private async Task QueueMostPlayedTrackAsync(GuildTopTrackSummary track)
    {
        await ExecuteActionAsync(ct => QueueMostPlayedTrackCoreAsync(track, playNext: false, ct));
    }

    private async Task PlayMostPlayedTrackNextAsync(GuildTopTrackSummary track)
    {
        await ExecuteActionAsync(ct => QueueMostPlayedTrackCoreAsync(track, playNext: true, ct));
    }

    private async Task SaveMostPlayedTrackFavoriteAsync(GuildTopTrackSummary track)
    {
        if (track.IsFavorited && track.FavoriteId is { } favoriteId)
        {
            await ExecuteActionAsync(async ct =>
            {
                var actionResult = await MusicFavoritesService.RemoveAsync((long)UserId!.Value, favoriteId, ct);
                await RefreshFavoritesAsync(ct);
                await RefreshMostPlayedAsync(ct);
                return actionResult;
            });

            return;
        }

        await ExecuteActionAsync(async ct =>
        {
            var lavalinkTrack = CreateStatsTrack(track);
            var actionResult = await MusicFavoritesService.AddSongAsync((long)UserId!.Value, lavalinkTrack, ct);
            await RefreshFavoritesAsync(ct);
            await RefreshMostPlayedAsync(ct);
            return actionResult;
        });
    }

    private async Task RefreshMostPlayedAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
        {
            MostPlayedTracks = [];
            return;
        }

        IsLoadingMostPlayed = true;

        try
        {
            MostPlayedTracks = await MusicGuildStatsService.GetTopTracksAsync(Session.Channel.GuildId, cancellationToken: cancellationToken);
            ApplyFavoriteStateToMostPlayedTracks();
        }
        finally
        {
            IsLoadingMostPlayed = false;
        }
    }

    private void ApplyFavoriteStateToMostPlayedTracks()
    {
        if (MostPlayedTracks.Count == 0)
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

        MostPlayedTracks = MostPlayedTracks.Select(track =>
        {
            var externalId = !string.IsNullOrWhiteSpace(track.SourceUrl)
                ? track.SourceUrl
                : $"{track.SourceName}:{track.Title}:{track.Artist}";

            return songFavoritesByExternalId.TryGetValue(externalId, out var favorite)
                ? track with { IsFavorited = true, FavoriteId = favorite.Id }
                : track with { IsFavorited = false, FavoriteId = null };
        }).ToArray();
    }

    private async Task<MusicCommandResult> QueueMostPlayedTrackCoreAsync(GuildTopTrackSummary track, bool playNext, CancellationToken cancellationToken)
    {
        var lavalinkTrack = CreateStatsTrack(track);

        return playNext
            ? await MusicDashboardQueueService.PlayNextTrackAsync(UserId!.Value, lavalinkTrack, cancellationToken)
            : await MusicDashboardQueueService.QueueTrackAsync(UserId!.Value, lavalinkTrack, cancellationToken);
    }

    private static Lavalink4NET.Tracks.LavalinkTrack CreateStatsTrack(GuildTopTrackSummary track)
    {
        var identifier = !string.IsNullOrWhiteSpace(track.SourceUrl)
            ? track.SourceUrl
            : $"{track.Title}-{track.Artist}";

        return new Lavalink4NET.Tracks.LavalinkTrack
        {
            Identifier = identifier,
            Title = track.Title,
            Author = track.Artist,
            SourceName = track.SourceName,
            Uri = Uri.TryCreate(track.SourceUrl, UriKind.Absolute, out var songUri) ? songUri : null,
            ArtworkUri = Uri.TryCreate(track.ArtworkUrl, UriKind.Absolute, out var artworkUri) ? artworkUri : null,
            IsSeekable = true,
        };
    }
}
