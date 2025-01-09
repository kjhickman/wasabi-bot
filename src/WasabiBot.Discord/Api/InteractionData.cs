using System.Text.Json.Serialization;

namespace WasabiBot.Discord.Api;

public class DiscordInteractionData
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("options")]
    public InteractionDataOption[]? Options { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }
}