using System.Text.Json.Serialization;

namespace VinceBot.Discord;

public class ApplicationCommandChoice
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    public object Value { get; set; }
}
