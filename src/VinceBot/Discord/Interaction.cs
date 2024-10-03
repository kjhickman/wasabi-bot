using System.Text.Json.Serialization;
using VinceBot.Discord.Enums;

namespace VinceBot.Discord;

/// <summary>
///     <a href="https://discord.com/developers/docs/interactions/receiving-and-responding#interaction-object">Source</a>
/// </summary>
public class Interaction
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("application_id")]
    public string ApplicationId { get; set; } = null!;

    [JsonPropertyName("type")]
    public InteractionType Type { get; set; }

    [JsonPropertyName("data")]
    public InteractionData? Data { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("member")]
    public GuildMember? GuildMember { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }
}
