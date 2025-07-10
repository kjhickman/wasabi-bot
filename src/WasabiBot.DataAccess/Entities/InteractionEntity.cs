namespace WasabiBot.DataAccess.Entities;

public sealed class InteractionEntity
{
    public long Id { get; init; }
    public long ChannelId { get; init; }
    public long ApplicationId { get; init; }
    public long UserId { get; init; }
    public long? GuildId { get; init; }
    public required string Username { get; init; }
    public string? GlobalName { get; init; }
    public string? Nickname { get; init; }
    public string? Data { get; init; }
    public DateTime CreatedAt { get; init; }

}
