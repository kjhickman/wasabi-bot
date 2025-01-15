using System.Text.Json.Serialization;
using Discord;

namespace WasabiBot.Discord.Api;

public class Entitlement
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("sku_id")]
    public required string SkuId { get; set; }

    [JsonPropertyName("application_id")]
    public required string ApplicationId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("type")]
    public EntitlementType Type { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
    
    [JsonPropertyName("consumed")]
    public bool? Consumed { get; set; }

    [JsonPropertyName("starts_at")]
    public DateTimeOffset StartsAt { get; set; }

    [JsonPropertyName("ends_at")]
    public DateTimeOffset EndsAt { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }
}