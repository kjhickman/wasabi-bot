using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface IReminderService
{
    Task<bool> ScheduleAsync(ulong userId, ulong channelId, string reminder, DateTimeOffset remindAt);
    Task<List<ReminderEntity>> GetAllUnsent(CancellationToken ct = default);
    Task<IReadOnlyCollection<long>> SendRemindersAsync(IEnumerable<ReminderEntity> reminders, CancellationToken ct = default);
}

