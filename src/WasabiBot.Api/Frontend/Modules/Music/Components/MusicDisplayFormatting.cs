namespace WasabiBot.Api.Frontend.Modules.Music;

internal static class MusicDisplayFormatting
{
    public static string FormatSourceName(string sourceName)
    {
        return sourceName.ToLowerInvariant() switch
        {
            "scsearch" => "SoundCloud",
            "soundcloud" => "SoundCloud",
            "http" => "Direct stream",
            _ => sourceName
        };
    }

    public static string FormatStationTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return string.Empty;
        }

        var parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" • ", parts.Take(3));
    }
}
