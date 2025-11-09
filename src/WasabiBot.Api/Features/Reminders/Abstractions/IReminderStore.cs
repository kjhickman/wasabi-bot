using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface IReminderStore
{
    DateTimeOffset? GetNextDueTime();
    List<ReminderEntity> GetAllDueReminders();
    void InsertMany(IEnumerable<ReminderEntity> reminders);
    void Insert(ReminderEntity entity);
    void RemoveById(long id);
    Task WaitForEarlierAsync(CancellationToken ct);
}

