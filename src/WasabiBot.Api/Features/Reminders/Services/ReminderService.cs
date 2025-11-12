using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class ReminderService : IReminderService
{
    private readonly WasabiBotContext _ctx;
    private readonly IReminderStore _store;
    private readonly RestClient _discordClient;
    private readonly ILogger<ReminderService> _logger;
    private readonly Tracer _tracer;
    private readonly TimeProvider _timeProvider;

    public ReminderService(WasabiBotContext ctx, IReminderStore store, RestClient discordClient,
        ILogger<ReminderService> logger, Tracer tracer, TimeProvider timeProvider)
    {
        _ctx = ctx;
        _store = store;
        _discordClient = discordClient;
        _logger = logger;
        _tracer = tracer;
        _timeProvider = timeProvider;
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
            CreatedAt = _timeProvider.GetUtcNow(),
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

    public async Task<List<ReminderEntity>> GetAllByUserId(long userId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.by_user");
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsReminderSent)
            .OrderBy(r => r.RemindAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<long>> SendRemindersAsync(IEnumerable<ReminderEntity> reminders, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.send");
        ConcurrentBag<long> sentReminderIds = [];
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
            .Where(r => r.IsReminderSent == false && sentReminderIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsReminderSent, r => true), ct);

        return sentReminderIds;
    }

    public async Task<ReminderEntity?> GetByIdAsync(long reminderId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.get_by_id");

        return await _ctx.Reminders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct);
    }

    public async Task<bool> DeleteByIdAsync(long reminderId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.delete");

        var reminder = await _ctx.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct);

        if (reminder == null)
        {
            _logger.LogWarning("Attempted to delete reminder {ReminderId} but it does not exist", reminderId);
            return false;
        }

        _ctx.Reminders.Remove(reminder);
        var deleted = await _ctx.SaveChangesAsync(ct) > 0;

        if (deleted)
        {
            _store.RemoveById(reminder.Id);
            _logger.LogInformation("Deleted reminder {ReminderId}", reminderId);
        }

        return deleted;
    }
}

