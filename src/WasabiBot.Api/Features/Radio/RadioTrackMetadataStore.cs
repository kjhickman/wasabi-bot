using System.Collections.Concurrent;
using Lavalink4NET.Tracks;

namespace WasabiBot.Api.Features.Radio;

internal sealed class RadioTrackMetadataStore
{
    private readonly ConcurrentDictionary<string, RadioTrackMetadata> _entries = new();

    public void Set(LavalinkTrack track, string title)
    {
        _entries[BuildKey(track)] = new RadioTrackMetadata(title);
    }

    public bool TryGet(LavalinkTrack track, out RadioTrackMetadata metadata)
    {
        return _entries.TryGetValue(BuildKey(track), out metadata!);
    }

    private static string BuildKey(LavalinkTrack track)
    {
        return $"{track.Identifier}|{track.Uri}";
    }
}

internal sealed record RadioTrackMetadata(string Title);
