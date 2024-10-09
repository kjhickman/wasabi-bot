using System.Text.Json;
using System.Text.Json.Serialization;
using WasabiBot.Discord.Enums;

namespace WasabiBot.Discord;

public class InteractionDataOption
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public ApplicationCommandOptionType Type { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("options")]
    public IEnumerable<InteractionDataOption>? Options { get; set; }

    [JsonPropertyName("focused")]
    public bool? Focused { get; set; }
}
