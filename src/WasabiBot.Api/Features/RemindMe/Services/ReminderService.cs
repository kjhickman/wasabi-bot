using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderService : IReminderService
{
    private readonly WasabiBotContext _ctx;
    private readonly PendingReminderStore _store;
    private readonly RestClient _discordClient;
    private readonly ILogger<ReminderService> _logger;
    private readonly Tracer _tracer;

    public ReminderService(WasabiBotContext ctx, PendingReminderStore store, RestClient discordClient,
        ILogger<ReminderService> logger, Tracer tracer)
    {
        _ctx = ctx;
        _store = store;
        _discordClient = discordClient;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<bool> ScheduleAsync(ulong userId, ulong channelId, string reminder, DateTimeOffset remindAt)
    {
        using var span = _tracer.StartActiveSpan("reminder.schedule");
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
            _store.Insert(entity);
        }
        return saved;
    }

    public async Task<List<ReminderEntity>> GetAllUnsent(CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.unsent");
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => !r.IsReminderSent)
            .OrderBy(r => r.RemindAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<long>> SendRemindersAsync(IEnumerable<ReminderEntity> reminders, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.send");
        ConcurrentBag<long> sentReminderIds = new();
        await Parallel.ForEachAsync(reminders, ct, async (reminder, token) =>
        {
            try
            {
                await _discordClient.SendMessageAsync((ulong)reminder.ChannelId,
                    $"<@{reminder.UserId}> Reminder: {reminder.ReminderMessage}", cancellationToken: token);
                sentReminderIds.Add(reminder.Id);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to send reminder {ReminderId} to user {UserId} in channel {ChannelId}: {Error}",
                    reminder.Id, reminder.UserId, reminder.ChannelId, e.Message);
            }
        });

        using var updateSpan = _tracer.StartActiveSpan("reminder.send.update_db");
        await _ctx.Reminders
            .Where(r => sentReminderIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsReminderSent, r => true), ct);

        return sentReminderIds;
    }
}
