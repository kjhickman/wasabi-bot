using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Frontend.Modules.Music;

public partial class MusicShell
{
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
}
