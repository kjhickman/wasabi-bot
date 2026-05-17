namespace WasabiBot.Api.Persistence.Entities;

public class ReminderEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ChannelId { get; set; }
    public required string ReminderMessage { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ReminderStatus Status { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}

public enum ReminderStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Canceled = 4
}
