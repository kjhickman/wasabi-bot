namespace WasabiBot.Api.Features.Music;

internal interface IMusicDashboardService
{
    Task<ActiveMusicSession?> GetActiveSessionAsync(ulong userId, CancellationToken cancellationToken = default);
}
