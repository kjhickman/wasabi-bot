using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Components.Features.Music;

public partial class MusicShell : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private CancellationTokenSource? _refreshCancellationTokenSource;
    private Task? _refreshTask;

    [Parameter]
    public MusicPageKind ActivePage { get; set; }

    [Inject]
    private IAuthorizationService AuthorizationService { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IMusicDashboardService MusicDashboardService { get; set; } = default!;

    [Inject]
    private IMusicDashboardControlService MusicDashboardControlService { get; set; } = default!;

    [Inject]
    private IMusicDashboardSearchService MusicDashboardSearchService { get; set; } = default!;

    [Inject]
    private IMusicDashboardQueueService MusicDashboardQueueService { get; set; } = default!;

    [Inject]
    private IMusicFavoritesService MusicFavoritesService { get; set; } = default!;

    [Inject]
    private IMusicGuildStatsService MusicGuildStatsService { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    private bool IsAuthenticated { get; set; }
    private bool HasGuildAccess { get; set; }
    private ulong? UserId { get; set; }
    private string? LoadError { get; set; }
    private bool IsSubmittingAction { get; set; }
    private string SearchQuery { get; set; } = string.Empty;
    private bool IsSearching { get; set; }
    private bool HasSearchAttempt { get; set; }
    private string? SearchError { get; set; }
    private MusicDashboardSearchResults? SearchResults { get; set; }
    private MusicFavoritesSnapshot Favorites { get; set; } = new([], []);
    private bool IsLoadingFavorites { get; set; }
    private IReadOnlyList<GuildTopTrackSummary> MostPlayedTracks { get; set; } = [];
    private bool IsLoadingMostPlayed { get; set; }
    private bool IsLoading { get; set; } = true;
    private ActiveMusicSession? Session { get; set; }

    private string PageTitleText => !IsAuthenticated ? "Sign In" : HasGuildAccess ? PageHeading : "Access Restricted";
    private string LoginHref => BuildLoginHref();

    private string PageHeading => ActivePage switch
    {
        MusicPageKind.Live => "Music room",
        MusicPageKind.Search => "Music room",
        MusicPageKind.Library => "Music room",
        MusicPageKind.Stats => "Music room",
        _ => "Music"
    };

    private string PageDescription => ActivePage switch
    {
        MusicPageKind.Live => "Control playback, search tracks, and grow the room from one place.",
        MusicPageKind.Search => "Control playback, search tracks, and grow the room from one place.",
        MusicPageKind.Library => "Control playback, search tracks, and grow the room from one place.",
        MusicPageKind.Stats => "Control playback, search tracks, and grow the room from one place.",
        _ => "Music controls and discovery for Wasabi Bot."
    };

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated)
        {
            IsLoading = false;
            return;
        }

        HasGuildAccess = (await AuthorizationService.AuthorizeAsync(user, policyName: "DiscordGuildMember")).Succeeded;
        if (!HasGuildAccess)
        {
            IsLoading = false;
            return;
        }

        if (!TryGetUserId(user, out var userId))
        {
            LoadError = "Couldn't resolve your Discord identity for the music dashboard.";
            IsLoading = false;
            return;
        }

        UserId = userId;
        await RefreshSessionAsync();
        await EnsureActivePageDataAsync();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive || UserId is null)
        {
            return Task.CompletedTask;
        }

        _refreshCancellationTokenSource = new CancellationTokenSource();
        _refreshTask = RunRefreshLoopAsync(_refreshCancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_refreshCancellationTokenSource is not null)
        {
            await _refreshCancellationTokenSource.CancelAsync();
            _refreshCancellationTokenSource.Dispose();
        }

        if (_refreshTask is not null)
        {
            try
            {
                await _refreshTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        await RefreshSessionAsync(cancellationToken);
        await InvokeAsync(StateHasChanged);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshSessionAsync(cancellationToken);

            if (ActivePage == MusicPageKind.Stats && Session is not null)
            {
                await RefreshMostPlayedAsync(cancellationToken);
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task EnsureActivePageDataAsync(CancellationToken cancellationToken = default)
    {
        switch (ActivePage)
        {
            case MusicPageKind.Library:
                await RefreshFavoritesAsync(cancellationToken);
                break;
            case MusicPageKind.Stats when Session is not null:
                await RefreshMostPlayedAsync(cancellationToken);
                break;
        }
    }

    private async Task RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        if (UserId is null)
        {
            IsLoading = false;
            return;
        }

        try
        {
            Session = await MusicDashboardService.GetActiveSessionAsync(UserId.Value, cancellationToken);
            LoadError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            LoadError = "Couldn't load the music dashboard right now. Please try again in a moment.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TogglePauseAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.TogglePauseAsync(UserId!.Value, ct));
    }

    private async Task JoinUserChannelAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.JoinUserChannelAsync(UserId!.Value, ct));
    }

    private async Task SkipAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.SkipAsync(UserId!.Value, ct));
    }

    private async Task StopAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.StopAsync(UserId!.Value, ct));
    }

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

    private async Task RemoveQueueItemAsync(MusicQueueItemSnapshot item)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.RemoveQueueItemAsync(UserId!.Value, item.Position, ct));
    }

    private async Task MoveQueueItemUpAsync(MusicQueueItemSnapshot item)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.MoveQueueItemAsync(UserId!.Value, item.Position, item.Position - 1, ct));
    }

    private async Task MoveQueueItemDownAsync(MusicQueueItemSnapshot item)
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.MoveQueueItemAsync(UserId!.Value, item.Position, item.Position + 1, ct));
    }

    private async Task ClearQueueAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardQueueService.ClearQueueAsync(UserId!.Value, ct));
    }

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

    private async Task ExecuteActionAsync(Func<CancellationToken, Task<MusicCommandResult>> action)
    {
        if (UserId is null || IsSubmittingAction)
        {
            return;
        }

        IsSubmittingAction = true;

        try
        {
            await action(CancellationToken.None);
            await RefreshSessionAsync();

            if (ActivePage == MusicPageKind.Library)
            {
                await RefreshFavoritesAsync();
            }

            if (ActivePage == MusicPageKind.Stats && Session is not null)
            {
                await RefreshMostPlayedAsync();
            }
        }
        catch
        {
        }
        finally
        {
            IsSubmittingAction = false;
        }
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
        }
        finally
        {
            IsLoadingFavorites = false;
        }
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
        }
        finally
        {
            IsLoadingMostPlayed = false;
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
            var station = new WasabiBot.Api.Features.Radio.RadioBrowserStation
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

    private static bool TryGetUserId(ClaimsPrincipal user, out ulong userId)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return ulong.TryParse(userIdClaim, out userId);
    }

    private string BuildLoginHref()
    {
        var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/login-discord";
        }

        return $"/login-discord?returnUrl=/{Uri.EscapeDataString(relativePath)}";
    }

    private string GetMusicTabClass(MusicPageKind pageKind)
    {
        return pageKind == ActivePage ? "tab tab-active" : "tab";
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
