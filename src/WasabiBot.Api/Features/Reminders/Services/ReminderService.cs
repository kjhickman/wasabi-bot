using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Persistence;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class ReminderService : IReminderService
{
    private readonly WasabiBotContext _ctx;
    private readonly RestClient _discordClient;
    private readonly IReminderChangeNotifier _changeNotifier;
    private readonly ILogger<ReminderService> _logger;
    private readonly Tracer _tracer;
    private readonly TimeProvider _timeProvider;

    public ReminderService(WasabiBotContext ctx, RestClient discordClient, IReminderChangeNotifier changeNotifier,
        ILogger<ReminderService> logger, Tracer tracer, TimeProvider timeProvider)
    {
        _ctx = ctx;
        _discordClient = discordClient;
        _changeNotifier = changeNotifier;
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
            DueAt = remindAt,
            CreatedAt = _timeProvider.GetUtcNow(),
            Status = ReminderStatus.Pending,
            AttemptCount = 0
        };

        _ctx.Reminders.Add(entity);
        await _ctx.SaveChangesAsync();
        await _changeNotifier.NotifyReminderChangedAsync();

        return entity.Id > 0;
    }

    public async Task<List<ReminderEntity>> GetAllUnsent(CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.unsent");
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => r.Status == ReminderStatus.Pending)
            .OrderBy(r => r.DueAt)
            .ToListAsync(ct);
    }

    public async Task<List<ReminderEntity>> GetAllByUserId(long userId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.by_user");
        return await _ctx.Reminders
            .AsNoTracking()
            .Where(r => r.UserId == userId && (r.Status == ReminderStatus.Pending || r.Status == ReminderStatus.Processing))
            .OrderBy(r => r.DueAt)
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
        var ids = sentReminderIds.Distinct().ToArray();
        if (ids.Length > 0)
        {
            await _ctx.Reminders
                .Where(r => ids.Contains(r.Id) && r.Status == ReminderStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, ReminderStatus.Sent)
                    .SetProperty(r => r.SentAt, _timeProvider.GetUtcNow())
                    .SetProperty(r => r.LastError, (string?)null), ct);
        }

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

        var deleted = await _ctx.Reminders
            .Where(r => r.Id == reminderId && r.Status != ReminderStatus.Sent && r.Status != ReminderStatus.Canceled)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, ReminderStatus.Canceled)
                .SetProperty(r => r.LastError, (string?)null)
                .SetProperty(r => r.ClaimedAt, (DateTimeOffset?)null), ct);

        if (deleted == 0)
        {
            _logger.LogWarning("Attempted to cancel reminder {ReminderId} but it does not exist or is already finalized", reminderId);
            return false;
        }

        await _changeNotifier.NotifyReminderChangedAsync(ct);
        _logger.LogInformation("Canceled reminder {ReminderId}", reminderId);
        return true;
    }

    public async Task<IReadOnlyList<ReminderEntity>> ClaimDueBatchAsync(int batchSize, DateTimeOffset now, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.claim_due_batch");

        if (batchSize <= 0)
        {
            return [];
        }

        var connection = _ctx.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE "Reminders" AS r
                SET "Status" = @processingStatus,
                    "ClaimedAt" = @claimedAt,
                    "AttemptCount" = r."AttemptCount" + 1
                WHERE r."Id" IN (
                    SELECT candidate."Id"
                    FROM "Reminders" AS candidate
                    WHERE candidate."Status" = @pendingStatus
                      AND candidate."DueAt" <= @now
                    ORDER BY candidate."DueAt", candidate."Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT @batchSize
                )
                RETURNING r."Id", r."UserId", r."ChannelId", r."ReminderMessage", r."DueAt", r."CreatedAt", r."Status", r."ClaimedAt", r."SentAt", r."AttemptCount", r."LastError";
                """;

            AddParameter(command, "processingStatus", ReminderStatus.Processing.ToString());
            AddParameter(command, "pendingStatus", ReminderStatus.Pending.ToString());
            AddParameter(command, "claimedAt", now);
            AddParameter(command, "now", now);
            AddParameter(command, "batchSize", batchSize);

            var claimed = new List<ReminderEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                claimed.Add(new ReminderEntity
                {
                    Id = reader.GetInt64(0),
                    UserId = reader.GetInt64(1),
                    ChannelId = reader.GetInt64(2),
                    ReminderMessage = reader.GetString(3),
                    DueAt = reader.GetFieldValue<DateTimeOffset>(4),
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                    Status = Enum.Parse<ReminderStatus>(reader.GetString(6)),
                    ClaimedAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                    SentAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                    AttemptCount = reader.GetInt32(9),
                    LastError = reader.IsDBNull(10) ? null : reader.GetString(10)
                });
            }

            return claimed;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> MarkSentAsync(IEnumerable<long> reminderIds, DateTimeOffset sentAt, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.mark_sent");

        var ids = reminderIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        var updated = await _ctx.Reminders
            .Where(r => ids.Contains(r.Id) && r.Status == ReminderStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, ReminderStatus.Sent)
                .SetProperty(r => r.SentAt, sentAt)
                .SetProperty(r => r.LastError, (string?)null), ct);

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated;
    }

    public async Task<bool> MarkFailedAsync(long reminderId, string? error, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.mark_failed");

        var updated = await _ctx.Reminders
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, ReminderStatus.Failed)
                .SetProperty(r => r.LastError, error)
                .SetProperty(r => r.ClaimedAt, (DateTimeOffset?)null), ct);

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated > 0;
    }

    public async Task<bool> RequeueAsync(long reminderId, DateTimeOffset dueAt, string? error, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.requeue");

        var updated = await _ctx.Reminders
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, ReminderStatus.Pending)
                .SetProperty(r => r.DueAt, dueAt)
                .SetProperty(r => r.LastError, error)
                .SetProperty(r => r.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(r => r.SentAt, (DateTimeOffset?)null), ct);

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated > 0;
    }

    public Task<DateTimeOffset?> GetNextDueTimeAsync(CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.next_due");

        return _ctx.Reminders
            .AsNoTracking()
            .Where(r => r.Status == ReminderStatus.Pending)
            .OrderBy(r => r.DueAt)
            .Select(r => (DateTimeOffset?)r.DueAt)
            .FirstOrDefaultAsync(ct);
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
