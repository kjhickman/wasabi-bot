// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace WasabiBot.DataAccess.Entities;

using System.ComponentModel.DataAnnotations.Schema;

public sealed class InteractionEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; }
    public long ChannelId { get; set; }
    public long ApplicationId { get; set; }
    public long UserId { get; set; }
    public long? GuildId { get; set; }
    public required string Username { get; set; }
    public string? GlobalName { get; set; }
    public string? Nickname { get; set; }
    public string? Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
