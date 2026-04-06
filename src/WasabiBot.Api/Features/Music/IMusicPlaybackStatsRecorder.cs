using Lavalink4NET.Tracks;

namespace WasabiBot.Api.Features.Music;

internal interface IMusicPlaybackStatsRecorder
{
    Task RecordTrackStartedAsync(ulong guildId, LavalinkTrack track, CancellationToken cancellationToken = default);
}
