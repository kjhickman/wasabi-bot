using System.Collections.Concurrent;
using Dapper;
using NetCord.Rest;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class ReminderService : IReminderService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly RestClient _discordClient;
    private readonly IReminderChangeNotifier _changeNotifier;
    private readonly ILogger<ReminderService> _logger;
    private readonly Tracer _tracer;
    private readonly TimeProvider _timeProvider;

    public ReminderService(NpgsqlDataSource dataSource, RestClient discordClient, IReminderChangeNotifier changeNotifier,
        ILogger<ReminderService> logger, Tracer tracer, TimeProvider timeProvider)
    {
        _dataSource = dataSource;
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

        const string sql = """
            INSERT INTO "Reminders" ("UserId", "ChannelId", "ReminderMessage", "DueAt", "CreatedAt", "Status", "AttemptCount")
            VALUES (@UserId, @ChannelId, @ReminderMessage, @DueAt, @CreatedAt, @Status, @AttemptCount)
            RETURNING "Id"
            """;

        await using var connection = await _dataSource.OpenConnectionAsync();
        entity.Id = await connection.ExecuteScalarAsync<long>(sql, new
        {
            entity.UserId,
            entity.ChannelId,
            entity.ReminderMessage,
            entity.DueAt,
            entity.CreatedAt,
            Status = entity.Status.ToString(),
            entity.AttemptCount,
        });
        await _changeNotifier.NotifyReminderChangedAsync();

        return entity.Id > 0;
    }

    public async Task<List<ReminderEntity>> GetAllUnsent(CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.unsent");
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<ReminderRow>(new CommandDefinition(
            SelectReminderSql + " WHERE \"Status\" = @Status ORDER BY \"DueAt\"",
            new { Status = ReminderStatus.Pending.ToString() }, cancellationToken: ct));
        return rows.Select(row => row.ToEntity()).ToList();
    }

    public async Task<List<ReminderEntity>> GetAllByUserId(long userId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.list.by_user");
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<ReminderRow>(new CommandDefinition(
            SelectReminderSql + " WHERE \"UserId\" = @UserId AND \"Status\" IN (@PendingStatus, @ProcessingStatus) ORDER BY \"DueAt\"",
            new { UserId = userId, PendingStatus = ReminderStatus.Pending.ToString(), ProcessingStatus = ReminderStatus.Processing.ToString() }, cancellationToken: ct));
        return rows.Select(row => row.ToEntity()).ToList();
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
            await MarkSentAsync(ids, _timeProvider.GetUtcNow(), ct);
        }

        return sentReminderIds;
    }

    public async Task<ReminderEntity?> GetByIdAsync(long reminderId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.get_by_id");

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<ReminderRow>(new CommandDefinition(
            SelectReminderSql + " WHERE \"Id\" = @ReminderId",
            new { ReminderId = reminderId }, cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<bool> DeleteByIdAsync(long reminderId, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.delete");

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var deleted = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "Reminders"
            SET "Status" = @CanceledStatus,
                "LastError" = NULL,
                "ClaimedAt" = NULL
            WHERE "Id" = @ReminderId AND "Status" <> @SentStatus AND "Status" <> @CanceledStatus
            """, new
        {
            ReminderId = reminderId,
            SentStatus = ReminderStatus.Sent.ToString(),
            CanceledStatus = ReminderStatus.Canceled.ToString(),
        }, cancellationToken: ct));

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

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var claimed = await connection.QueryAsync<ReminderRow>(new CommandDefinition("""
                UPDATE "Reminders" AS r
                SET "Status" = @ProcessingStatus,
                    "ClaimedAt" = @ClaimedAt,
                    "AttemptCount" = r."AttemptCount" + 1
                WHERE r."Id" IN (
                    SELECT candidate."Id"
                    FROM "Reminders" AS candidate
                    WHERE candidate."Status" = @PendingStatus
                      AND candidate."DueAt" <= @Now
                    ORDER BY candidate."DueAt", candidate."Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT @BatchSize
                )
                RETURNING r."Id", r."UserId", r."ChannelId", r."ReminderMessage", r."DueAt", r."CreatedAt", r."Status", r."ClaimedAt", r."SentAt", r."AttemptCount", r."LastError";
                """, new
        {
            ProcessingStatus = ReminderStatus.Processing.ToString(),
            PendingStatus = ReminderStatus.Pending.ToString(),
            ClaimedAt = now,
            Now = now,
            BatchSize = batchSize,
        }, cancellationToken: ct));

        return claimed.Select(row => row.ToEntity()).ToArray();
    }

    public async Task<int> MarkSentAsync(IEnumerable<long> reminderIds, DateTimeOffset sentAt, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.mark_sent");

        var ids = reminderIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "Reminders"
            SET "Status" = @SentStatus,
                "SentAt" = @SentAt,
                "LastError" = NULL
            WHERE "Id" = ANY(@Ids) AND "Status" = @ProcessingStatus
            """, new { Ids = ids, SentStatus = ReminderStatus.Sent.ToString(), SentAt = sentAt, ProcessingStatus = ReminderStatus.Processing.ToString() }, cancellationToken: ct));

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated;
    }

    public async Task<bool> MarkFailedAsync(long reminderId, string? error, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.mark_failed");

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "Reminders"
            SET "Status" = @FailedStatus,
                "LastError" = @Error,
                "ClaimedAt" = NULL
            WHERE "Id" = @ReminderId
            """, new { ReminderId = reminderId, FailedStatus = ReminderStatus.Failed.ToString(), Error = error }, cancellationToken: ct));

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated > 0;
    }

    public async Task<bool> RequeueAsync(long reminderId, DateTimeOffset dueAt, string? error, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.requeue");

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "Reminders"
            SET "Status" = @PendingStatus,
                "DueAt" = @DueAt,
                "LastError" = @Error,
                "ClaimedAt" = NULL,
                "SentAt" = NULL
            WHERE "Id" = @ReminderId
            """, new { ReminderId = reminderId, PendingStatus = ReminderStatus.Pending.ToString(), DueAt = dueAt, Error = error }, cancellationToken: ct));

        if (updated > 0)
        {
            await _changeNotifier.NotifyReminderChangedAsync(ct);
        }

        return updated > 0;
    }

    public Task<DateTimeOffset?> GetNextDueTimeAsync(CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("reminder.next_due");

        return GetNextDueTimeCoreAsync(ct);
    }

    private async Task<DateTimeOffset?> GetNextDueTimeCoreAsync(CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        var nextDue = await connection.QueryFirstOrDefaultAsync<DateTime?>(new CommandDefinition("""
            SELECT "DueAt"
            FROM "Reminders"
            WHERE "Status" = @PendingStatus
            ORDER BY "DueAt"
            LIMIT 1
            """, new { PendingStatus = ReminderStatus.Pending.ToString() }, cancellationToken: ct));
        return nextDue is null ? null : new DateTimeOffset(nextDue.Value.ToUniversalTime());
    }

    private const string SelectReminderSql = """
        SELECT "Id", "UserId", "ChannelId", "ReminderMessage", "DueAt", "CreatedAt", "Status", "ClaimedAt", "SentAt", "AttemptCount", "LastError"
        FROM "Reminders"
        """;

    private sealed class ReminderRow
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long ChannelId { get; set; }
        public required string ReminderMessage { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string Status { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }

        public ReminderEntity ToEntity() => new()
        {
            Id = Id,
            UserId = UserId,
            ChannelId = ChannelId,
            ReminderMessage = ReminderMessage,
            DueAt = ToDateTimeOffset(DueAt),
            CreatedAt = ToDateTimeOffset(CreatedAt),
            Status = Enum.Parse<ReminderStatus>(Status),
            ClaimedAt = ClaimedAt is null ? null : ToDateTimeOffset(ClaimedAt.Value),
            SentAt = SentAt is null ? null : ToDateTimeOffset(SentAt.Value),
            AttemptCount = AttemptCount,
            LastError = LastError,
        };

        private static DateTimeOffset ToDateTimeOffset(DateTime value) => new(value.ToUniversalTime());
    }
}
