using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.RemindMe.Abstractions;

public interface IReminderService
{
    Task<bool> ScheduleAsync(ulong userId, ulong channelId, string reminder, DateTimeOffset remindAt);
    Task<List<ReminderEntity>> GetDueAsync(int take = 1000, CancellationToken cancellationToken = default);
    Task SendReminderAsync(ReminderEntity reminder);
}
