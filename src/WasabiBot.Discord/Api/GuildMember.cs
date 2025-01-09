using System.Text.Json.Serialization;

namespace WasabiBot.Discord.Api;

public class GuildMember
{
    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("nick")]
    public string? Nickname { get; set; }

    [JsonPropertyName("avatar")]
    public string? AvatarHash { get; set; }

    [JsonPropertyName("roles")]
    public required string[] RoleIds { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset JoinedAt { get; set; }

    [JsonPropertyName("premium_since")]
    public DateTimeOffset? PremiumSince { get; set; }

    [JsonPropertyName("deaf")]
    public bool Deafened { get; set; }

    [JsonPropertyName("mute")]
    public bool Muted { get; set; }

    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }

    [JsonPropertyName("pending")]
    public bool? Pending { get; set; }

    [JsonPropertyName("communication_disabled_until")]
    public DateTimeOffset? CommunicationDisabledUntil { get; set; }

    // todo: implement flags
    // public int Flags { get; set; }
}