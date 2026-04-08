using System.Security.Claims;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
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

        HasGuildAccess = (await AuthorizationService.AuthorizeAsync(user, resource: null, policyName: "DiscordGuildMember")).Succeeded;
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
                await RefreshFavoritesAsync(cancellationToken);
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
                await RefreshFavoritesAsync(cancellationToken);
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
}
