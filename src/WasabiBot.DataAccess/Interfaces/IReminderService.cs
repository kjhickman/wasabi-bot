namespace WasabiBot.DataAccess.Interfaces;

using Entities;

public interface IReminderService
{
    Task<bool> CreateAsync(long userId, long channelId, string reminder, DateTimeOffset remindAt);
    Task<List<ReminderEntity>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task MarkSentAsync(IEnumerable<long> reminderIds, CancellationToken cancellationToken = default);
}
