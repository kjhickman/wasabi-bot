using System.Text.Json.Serialization;
using Discord;

namespace WasabiBot.Discord.Api;

public class Interaction
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("application_id")]
    public required string ApplicationId { get; set; }

    [JsonPropertyName("type")]
    public InteractionType Type { get; set; }

    [JsonPropertyName("data")]
    public DiscordInteractionData? Data { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("member")]
    public GuildMember? GuildMember { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("entitlements")]
    public Entitlement[] Entitlements { get; set; } = [];
}