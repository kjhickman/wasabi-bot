using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderService : IReminderService
{
    private readonly WasabiBotContext _ctx;
    private readonly InMemoryReminderWindow _window;
    private readonly RestClient _discordClient;


    public ReminderService(WasabiBotContext ctx, InMemoryReminderWindow window, RestClient discordClient)
    {
        _ctx = ctx;
        _window = window;
        _discordClient = discordClient;
    }

    public async Task<bool> ScheduleAsync(ulong userId, ulong channelId, string reminder, DateTimeOffset remindAt)
    {
        var entity = new ReminderEntity
        {
            UserId = (long)userId,
            ChannelId = (long)channelId,
            ReminderMessage = reminder,
            RemindAt = remindAt,
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };
        _ctx.Reminders.Add(entity);
        var saved = await _ctx.SaveChangesAsync() > 0;
        if (saved)
        {
            // Insert into in-memory window so it can be processed without waiting for refresh.
            _window.Insert(entity);
        }
        return saved;
    }

    public async Task<List<ReminderEntity>> GetDueAsync(int take = 1000, CancellationToken cancellationToken = default)
    {
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => !r.IsReminderSent)
            .OrderBy(r => r.RemindAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task SendReminderAsync(ReminderEntity reminder)
    {
        await _discordClient.SendMessageAsync((ulong)reminder.ChannelId,
            $"<@{reminder.UserId}> Reminder: {reminder.ReminderMessage}");

        await MarkSentAsync(reminder.Id);
    }

    private async Task MarkSentAsync(long reminderId)
    {
        await _ctx.Reminders
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsReminderSent, r => true));
    }
}
