namespace WasabiBot.Api.Persistence.Entities;

public sealed class ApiCredentialEntity
{
    public long Id { get; set; }
    public long OwnerDiscordUserId { get; set; }
    public required string Name { get; set; }
    public required string ClientId { get; set; }
    public required string SecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
