using WasabiBot.DataAccess.Entities;

namespace WasabiBot.TestShared.Builders;

/// <summary>Builder for creating ReminderEntity test data.</summary>
public static class ReminderEntityBuilder
{
    /// <summary>Creates a ReminderEntity with sensible defaults for testing.</summary>
    public static ReminderEntity Create(
        long id = 1,
        long userId = 111,
        long channelId = 222,
        string? reminderMessage = null,
        DateTimeOffset? remindAt = null,
        bool isReminderSent = false)
    {
        return new ReminderEntity
        {
            Id = id,
            UserId = userId,
            ChannelId = channelId,
            ReminderMessage = reminderMessage ?? "Test reminder",
            RemindAt = remindAt ?? DateTimeOffset.UtcNow.AddDays(1),
            IsReminderSent = isReminderSent
        };
    }
}
