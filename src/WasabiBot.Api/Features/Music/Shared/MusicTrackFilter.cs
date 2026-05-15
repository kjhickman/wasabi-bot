using Lavalink4NET.Tracks;

namespace WasabiBot.Api.Features.Music;

internal static class MusicTrackFilter
{
    public static bool IsPreview(LavalinkTrack track)
    {
        // SoundCloud previews have /preview/ in their internal identifier
        return track.SourceName == "soundcloud" && 
               track.Identifier.Contains("/preview/", StringComparison.OrdinalIgnoreCase);
    }
}
