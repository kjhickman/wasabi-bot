// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace WasabiBot.DataAccess.Entities;

public class InteractionEntity
{
    public ulong Id { get; init; }
    public ulong ChannelId { get; init; }
    public ulong ApplicationId { get; init; }
    public ulong UserId { get; init; }
    public ulong? GuildId { get; init; }
    public required string Username { get; init; }
    public string? GlobalName { get; init; }
    public string? Nickname { get; init; }
    public string? Data { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
