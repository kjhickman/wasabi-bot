using System.Text.Json.Serialization;
using WasabiBot.Discord.Enums;

namespace WasabiBot.Discord;

public class ApplicationCommandOption
{
    [JsonPropertyName("type")]
    public ApplicationCommandOptionType Type { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("choices")]
    public ApplicationCommandChoice[]? Choices { get; set; }

    [JsonPropertyName("options")]
    public ApplicationCommandOption[]? Options { get; set; }

    [JsonPropertyName("channel_types")]
    public ChannelType[]? ChannelTypes { get; set; }

    [JsonPropertyName("min_value")]
    public double? MinimumValue { get; set; }

    [JsonPropertyName("max_value")]
    public double? MaximumValue { get; set; }

    [JsonPropertyName("min_length")]
    public int? MinimumLength { get; set; }

    [JsonPropertyName("max_length")]
    public int? MaximumLength { get; set; }

    [JsonPropertyName("autocomplete")]
    public bool? AutocompleteEnabled { get; set; }
}
