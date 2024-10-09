using System.Text.Json;
using WasabiBot.Discord;

namespace WasabiBot.Database.Entities;

public class InteractionRecord
{
    public required int Type { get; init; }
    public string? Data { get; init; }
    public string? GuildId { get; init; }
    public string? ChannelId { get; init; }
    public string? MemberNickname { get; init; }
    public string? MemberAvatarHash { get; init; }
    public string[]? MemberRoleIds { get; init; }
    public DateTimeOffset? MemberJoinedAt { get; init; }
    public DateTimeOffset? MemberPremiumSince { get; init; }
    public bool? MemberDeafened { get; init; }
    public bool? MemberMuted { get; init; }
    public string? MemberPermissions { get; init; }
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string UserGlobalName { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }

    public static InteractionRecord Create(Interaction interaction)
    {
        return new InteractionRecord
        {
            Type = (int)interaction.Type,
            Data = interaction.Data != null ? JsonSerializer.Serialize(interaction.Data, JsonContext.Default.InteractionData) : null,
            GuildId = interaction.GuildId,
            ChannelId = interaction.ChannelId,
            MemberNickname = interaction.GuildMember?.Nickname,
            MemberAvatarHash = interaction.GuildMember?.AvatarHash,
            MemberRoleIds = interaction.GuildMember?.RoleIds,
            MemberJoinedAt = interaction.GuildMember?.JoinedAt,
            MemberPremiumSince = interaction.GuildMember?.PremiumSince,
            MemberDeafened = interaction.GuildMember?.Deafened,
            MemberMuted = interaction.GuildMember?.Muted,
            MemberPermissions = interaction.GuildMember?.Permissions,
            UserId = (interaction.User?.Id ?? interaction.GuildMember?.User?.Id)!,
            Username = (interaction.User?.Username ?? interaction.GuildMember?.User?.Username)!,
            UserGlobalName = (interaction.User?.GlobalName ?? interaction.GuildMember?.User?.GlobalName)!,
            Version = interaction.Version,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }
}
