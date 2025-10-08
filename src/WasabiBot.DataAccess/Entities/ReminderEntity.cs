// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace WasabiBot.DataAccess.Entities;

public sealed class ReminderEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ChannelId { get; set; }
    public required string ReminderMessage { get; set; }
    public DateTimeOffset RemindAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsReminderSent { get; set; }
}

