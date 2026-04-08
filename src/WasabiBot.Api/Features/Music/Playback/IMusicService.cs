using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Music;

internal interface IMusicService
{
    Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string input, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> SkipAsync(ICommandContext ctx, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> StopAsync(ICommandContext ctx, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> QueueAsync(ICommandContext ctx, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> NowPlayingAsync(ICommandContext ctx, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> LeaveAsync(ICommandContext ctx, CancellationToken cancellationToken = default);
}
