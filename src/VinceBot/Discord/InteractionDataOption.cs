using System.Text.Json.Serialization;
using VinceBot.Discord.Enums;

namespace VinceBot.Discord;

public class InteractionDataOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public ApplicationCommandOptionType Type { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("options")]
    public IEnumerable<InteractionDataOption>? Options { get; set; }
}
