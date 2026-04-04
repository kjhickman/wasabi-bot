using Lavalink4NET.Tracks;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Features.Music;

internal interface IMusicFavoritesService
{
    Task<MusicFavoritesSnapshot> ListAsync(long discordUserId, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> AddSongAsync(long discordUserId, LavalinkTrack track, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> AddRadioAsync(long discordUserId, RadioBrowserStation station, CancellationToken cancellationToken = default);
    Task<MusicCommandResult> RemoveAsync(long discordUserId, long favoriteId, CancellationToken cancellationToken = default);
}
