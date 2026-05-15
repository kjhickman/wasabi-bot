using Dapper;
using Npgsql;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.IntegrationTests.Infrastructure;

/// <summary>
/// Provides reusable assertion helpers for integration tests.
/// </summary>
public static class TestAssertions
{
    /// <summary>Asserts that an interaction exists in the database.</summary>
    public static async Task AssertInteractionExistsAsync(NpgsqlDataSource dataSource, long interactionId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var exists = await connection.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (SELECT 1 FROM "Interactions" WHERE "Id" = @InteractionId)
            """, new { InteractionId = interactionId });
        await Assert.That(exists).IsTrue();
    }

    /// <summary>Counts total interactions in the database.</summary>
    public static async Task<int> CountInteractionsAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM \"Interactions\"");
    }

    /// <summary>Asserts that a reminder exists for the given user and channel.</summary>
    public static async Task AssertReminderExistsAsync(NpgsqlDataSource dataSource, long userId, long channelId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var exists = await connection.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (SELECT 1 FROM "Reminders" WHERE "UserId" = @UserId AND "ChannelId" = @ChannelId)
            """, new { UserId = userId, ChannelId = channelId });
        await Assert.That(exists).IsTrue();
    }

    /// <summary>Gets the first reminder in the database.</summary>
    public static async Task<ReminderEntity?> GetFirstReminderAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QueryFirstOrDefaultAsync<ReminderRow>(SelectReminderSql + " ORDER BY \"Id\" LIMIT 1");
        return row?.ToEntity();
    }

    /// <summary>Counts total reminders in the database.</summary>
    public static async Task<int> CountRemindersAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM \"Reminders\"");
    }

    public static async Task InsertReminderAsync(NpgsqlDataSource dataSource, ReminderEntity reminder)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO "Reminders" ("Id", "UserId", "ChannelId", "ReminderMessage", "DueAt", "CreatedAt", "Status", "ClaimedAt", "SentAt", "AttemptCount", "LastError")
            VALUES (@Id, @UserId, @ChannelId, @ReminderMessage, @DueAt, @CreatedAt, @Status, @ClaimedAt, @SentAt, @AttemptCount, @LastError)
            """, new
        {
            reminder.Id,
            reminder.UserId,
            reminder.ChannelId,
            reminder.ReminderMessage,
            reminder.DueAt,
            reminder.CreatedAt,
            Status = reminder.Status.ToString(),
            reminder.ClaimedAt,
            reminder.SentAt,
            reminder.AttemptCount,
            reminder.LastError,
        });
    }

    public static Task InsertRemindersAsync(NpgsqlDataSource dataSource, params ReminderEntity[] reminders)
    {
        return Task.WhenAll(reminders.Select(reminder => InsertReminderAsync(dataSource, reminder)));
    }

    public static async Task<ReminderEntity> GetReminderAsync(NpgsqlDataSource dataSource, long id)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QuerySingleAsync<ReminderRow>(SelectReminderSql + " WHERE \"Id\" = @Id", new { Id = id });
        return row.ToEntity();
    }

    public static async Task<ReminderEntity[]> GetRemindersAsync(NpgsqlDataSource dataSource, params long[] ids)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<ReminderRow>(SelectReminderSql + " WHERE \"Id\" = ANY(@Ids) ORDER BY \"Id\"", new { Ids = ids });
        return rows.Select(row => row.ToEntity()).ToArray();
    }

    public static async Task<ReminderEntity[]> GetActiveRemindersAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<ReminderRow>(SelectReminderSql + " WHERE \"Status\" <> @CanceledStatus ORDER BY \"Id\"", new { CanceledStatus = ReminderStatus.Canceled.ToString() });
        return rows.Select(row => row.ToEntity()).ToArray();
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
