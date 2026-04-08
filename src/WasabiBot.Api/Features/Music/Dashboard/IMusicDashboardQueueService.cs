using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

internal interface IMusicDashboardQueueService
{
    Task<MusicCommandResult> QueueTrackAsync(ulong userId, LavalinkTrack track, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> PlayNextTrackAsync(ulong userId, LavalinkTrack track, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> QueueStationAsync(ulong userId, RadioBrowserStation station, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> PlayNextStationAsync(ulong userId, RadioBrowserStation station, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> RemoveQueueItemAsync(ulong userId, int position, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> MoveQueueItemAsync(ulong userId, int position, int newPosition, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> ClearQueueAsync(ulong userId, CancellationToken cancellationToken = default);
}
