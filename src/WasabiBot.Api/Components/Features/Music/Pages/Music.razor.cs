using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Components.Features.Music;

public partial class Music : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private CancellationTokenSource? _refreshCancellationTokenSource;
    private Task? _refreshTask;

    [Inject]
    private IAuthorizationService AuthorizationService { get; set; } = default!;

    [Inject]
    private IMusicDashboardService MusicDashboardService { get; set; } = default!;

    [Inject]
    private IMusicDashboardControlService MusicDashboardControlService { get; set; } = default!;

    [Inject]
    private IMusicDashboardSearchService MusicDashboardSearchService { get; set; } = default!;

    [Inject]
    private IMusicDashboardQueueService MusicDashboardQueueService { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    private bool IsAuthenticated { get; set; }
    private bool HasGuildAccess { get; set; }
    private ulong? UserId { get; set; }
    private string? DisplayName { get; set; }
    private string? LoadError { get; set; }
    private string? ActionMessage { get; set; }
    private bool ActionIsError { get; set; }
    private bool IsSubmittingAction { get; set; }
    private string SearchQuery { get; set; } = string.Empty;
    private bool IsSearching { get; set; }
    private string? SearchError { get; set; }
    private MusicDashboardSearchResults? SearchResults { get; set; }
    private bool IsLoading { get; set; } = true;
    private ActiveMusicSession? Session { get; set; }
    private string PageTitleText => !IsAuthenticated ? "Sign In" : HasGuildAccess ? "Music Dashboard" : "Access Restricted";

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

        DisplayName = user.DisplayName;
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
            await InvokeAsync(StateHasChanged);
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

    private static bool TryGetUserId(ClaimsPrincipal user, out ulong userId)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return ulong.TryParse(userIdClaim, out userId);
    }

    private async Task TogglePauseAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.TogglePauseAsync(UserId!.Value, ct));
    }

    private async Task SkipAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.SkipAsync(UserId!.Value, ct));
    }

    private async Task StopAsync()
    {
        await ExecuteActionAsync(ct => MusicDashboardControlService.StopAsync(UserId!.Value, ct));
    }

    private async Task SearchAsync()
    {
        if (IsSearching)
        {
            return;
        }

        IsSearching = true;
        SearchError = null;

        try
        {
            SearchResults = await MusicDashboardSearchService.SearchAsync(SearchQuery, CancellationToken.None);
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

    private async Task ExecuteActionAsync(Func<CancellationToken, Task<MusicCommandResult>> action)
    {
        if (UserId is null || IsSubmittingAction)
        {
            return;
        }

        IsSubmittingAction = true;

        try
        {
            var result = await action(CancellationToken.None);
            ActionMessage = result.Message;
            ActionIsError = result.Ephemeral;
            await RefreshSessionAsync();
        }
        catch
        {
            ActionMessage = "Couldn't complete that music action right now. Please try again.";
            ActionIsError = true;
        }
        finally
        {
            IsSubmittingAction = false;
        }
    }
}
