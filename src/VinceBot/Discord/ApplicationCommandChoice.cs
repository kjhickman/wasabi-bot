using System.Text.Json.Serialization;

namespace VinceBot.Discord;

public class ApplicationCommandChoice
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public required object Value { get; set; }
}
