namespace WasabiBot.Api.Features.Music;

internal interface IMusicDashboardControlService
{
    Task<MusicCommandResult> TogglePauseAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> SkipAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> StopAsync(ulong userId, CancellationToken cancellationToken = default);
}
