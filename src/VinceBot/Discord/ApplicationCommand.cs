using System.Text.Json.Serialization;
using VinceBot.Discord.Enums;

namespace VinceBot.Discord;

/// <summary>
///     <a href="https://discord.com/developers/docs/interactions/application-commands#application-command-object">Source</a>
/// </summary>
public class ApplicationCommand
{
    [JsonPropertyName("id")]
    public ulong? Id { get; set; }

    [JsonPropertyName("type")]
    public ApplicationCommandType? Type { get; set; }

    [JsonPropertyName("application_id")]
    public ulong? ApplicationId { get; set; }

    [JsonPropertyName("guild_id")]
    public ulong? GuildId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("options")]
    public ApplicationCommandOption[]? Options { get; set; }

    [JsonPropertyName("default_member_permissions")]
    public string? DefaultMemberPermissions { get; set; }

    [JsonPropertyName("dm_permission")]
    public bool? DirectMessagePermission { get; set; }

    [JsonPropertyName("version")]
    public ulong? Version { get; set; }
}
