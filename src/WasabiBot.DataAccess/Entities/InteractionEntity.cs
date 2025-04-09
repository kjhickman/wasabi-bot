// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace WasabiBot.DataAccess.Entities;

public class InteractionEntity
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public required string ApplicationId { get; init; }
    public required string UserId { get; init; }
    public string? GuildId { get; init; }
    public required string Username { get; init; }
    public string? GlobalName { get; init; }
    public string? Nickname { get; init; }
    public string? Data { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
