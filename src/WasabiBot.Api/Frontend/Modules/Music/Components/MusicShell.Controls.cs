using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
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
}
