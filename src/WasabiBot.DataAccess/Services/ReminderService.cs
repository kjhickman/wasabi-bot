using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public sealed class ReminderService : IReminderService
{
    private readonly WasabiBotContext _ctx;

    public ReminderService(WasabiBotContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<bool> CreateAsync(long userId, long channelId, string reminder, DateTimeOffset remindAt)
    {
        var entity = new ReminderEntity
        {
            UserId = userId,
            ChannelId = channelId,
            ReminderMessage = reminder,
            RemindAt = remindAt,
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };
        _ctx.Reminders.Add(entity);
        var saved = await _ctx.SaveChangesAsync() > 0;
        return saved;
    }

    public async Task<List<ReminderEntity>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => !r.IsReminderSent && r.RemindAt <= now)
            .OrderBy(r => r.RemindAt)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSentAsync(IEnumerable<long> reminderIds, CancellationToken cancellationToken = default)
    {
        var ids = reminderIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        await _ctx.Reminders
            .Where(r => !r.IsReminderSent && ids.Contains(r.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsReminderSent, r => true), cancellationToken);
    }
}
