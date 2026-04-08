using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

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

    private string GetMusicTabClass(MusicPageKind pageKind)
    {
        return pageKind == ActivePage ? "tab tab-active" : "tab";
    }
}
