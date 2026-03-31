using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface IReminderService
{
    Task<bool> ScheduleAsync(ulong userId, ulong channelId, string reminder, DateTimeOffset remindAt);
    Task<List<ReminderEntity>> GetAllUnsent(CancellationToken ct = default);
    Task<List<ReminderEntity>> GetAllByUserId(long userId, CancellationToken ct = default);
    Task<ReminderEntity?> GetByIdAsync(long reminderId, CancellationToken ct = default);
    Task<IReadOnlyList<ReminderEntity>> ClaimDueBatchAsync(int batchSize, DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyCollection<long>> SendRemindersAsync(IEnumerable<ReminderEntity> reminders, CancellationToken ct = default);
    Task<int> MarkSentAsync(IEnumerable<long> reminderIds, DateTimeOffset sentAt, CancellationToken ct = default);
    Task<bool> MarkFailedAsync(long reminderId, string? error, CancellationToken ct = default);
    Task<bool> RequeueAsync(long reminderId, DateTimeOffset dueAt, string? error, CancellationToken ct = default);
    Task<DateTimeOffset?> GetNextDueTimeAsync(CancellationToken ct = default);
    Task<bool> DeleteByIdAsync(long reminderId, CancellationToken ct = default);
}
