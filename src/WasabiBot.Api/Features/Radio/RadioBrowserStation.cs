using System.Text.Json.Serialization;

namespace WasabiBot.Api.Features.Radio;

public sealed record RadioBrowserStation
{
    [JsonPropertyName("stationuuid")]
    public string StationUuid { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("url_resolved")]
    public string UrlResolved { get; init; } = string.Empty;

    [JsonPropertyName("homepage")]
    public string Homepage { get; init; } = string.Empty;

    [JsonPropertyName("favicon")]
    public string Favicon { get; init; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public string Tags { get; init; } = string.Empty;

    [JsonPropertyName("votes")]
    public int Votes { get; init; }

    [JsonPropertyName("clickcount")]
    public int ClickCount { get; init; }

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; init; }

    [JsonPropertyName("lastcheckok")]
    public int LastCheckOk { get; init; }
}
