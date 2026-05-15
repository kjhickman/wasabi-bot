using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Persistence.Entities;
using WasabiBot.IntegrationTests.Infrastructure;
using WasabiBot.TestShared.Builders;
using WasabiBot.TestShared.Infrastructure;

namespace WasabiBot.IntegrationTests.Features.Reminders;

/// <summary>
/// Integration tests for ReminderService.
/// Tests CRUD operations against a real PostgreSQL database.
/// Note: RestClient is sealed and cannot be mocked, so we pass null.
/// Tests that require Discord functionality should be in a separate test class.
/// </summary>
public class ReminderServiceTests : IntegrationTestBase
{
    private ReminderService CreateService(IReminderChangeNotifier? changeNotifier = null)
    {
        return new ReminderService(
            new TestDbConnectionFactory(CreateDataSource()),
            null!, // RestClient is sealed and not needed for database tests
            changeNotifier ?? new NoOpReminderChangeNotifier(),
            NullLogger<ReminderService>.Instance,
            TracerProvider.Default.GetTracer("test"),
            TimeProvider.System);
    }

    private sealed class NoOpReminderChangeNotifier : IReminderChangeNotifier
    {
        public Task NotifyReminderChangedAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private Task InsertRemindersAsync(params ReminderEntity[] reminders) => TestAssertions.InsertRemindersAsync(CreateDataSource(), reminders);

    private Task<ReminderEntity> GetReminderAsync(long id) => TestAssertions.GetReminderAsync(CreateDataSource(), id);

    private Task<ReminderEntity[]> GetRemindersAsync(params long[] ids) => TestAssertions.GetRemindersAsync(CreateDataSource(), ids);

    private Task<ReminderEntity[]> GetActiveRemindersAsync() => TestAssertions.GetActiveRemindersAsync(CreateDataSource());

    [Test]
    public async Task ScheduleAsync_ShouldInsertReminderIntoDatabase()
    {
        // Arrange
        var service = CreateService();
        var remindAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var result = await service.ScheduleAsync(123, 456, "Test reminder", remindAt);

        // Assert
        await Assert.That(result).IsTrue();
        await TestAssertions.AssertReminderExistsAsync(CreateDataSource(), 123, 456);
    }

    [Test]
    public async Task ScheduleAsync_ShouldSetCorrectReminderProperties()
    {
        // Arrange
        var service = CreateService();
        var remindAt = DateTimeOffset.UtcNow.AddHours(2);
        const string message = "Don't forget to test!";

        // Act
        await service.ScheduleAsync(111, 222, message, remindAt);

        // Assert
        var reminder = await TestAssertions.GetFirstReminderAsync(CreateDataSource());
        await Assert.That(reminder).IsNotNull();
        await Assert.That(reminder!.UserId).IsEqualTo(111);
        await Assert.That(reminder.ChannelId).IsEqualTo(222);
        await Assert.That(reminder.ReminderMessage).IsEqualTo(message);
        await Assert.That(reminder.Status).IsEqualTo(ReminderStatus.Pending);
    }

    [Test]
    public async Task GetByIdAsync_ShouldRetrieveSingleReminderById()
    {
        // Arrange
        var service = CreateService();
        var remindAt = DateTimeOffset.UtcNow.AddHours(1);
        await service.ScheduleAsync(123, 456, "Test reminder", remindAt);

        var createdReminder = await TestAssertions.GetFirstReminderAsync(CreateDataSource());

        // Act
        var result = await service.GetByIdAsync(createdReminder!.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(123);
        await Assert.That(result.ChannelId).IsEqualTo(456);
        await Assert.That(result.ReminderMessage).IsEqualTo("Test reminder");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNullForNonExistentId()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(99999);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAllUnsent_ShouldRetrieveOnlyUnsentReminders()
    {
        // Arrange
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 2, status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 3, status: ReminderStatus.Sent));

        var service = CreateService();

        // Act
        var results = await service.GetAllUnsent();

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var reminder in results)
        {
            await Assert.That(reminder.Status).IsEqualTo(ReminderStatus.Pending);
        }
    }

    [Test]
    public async Task GetAllUnsent_ShouldReturnEmptyListWhenNoUnsentReminders()
    {
        // Arrange
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, status: ReminderStatus.Sent),
            ReminderEntityBuilder.Create(id: 2, status: ReminderStatus.Sent));

        var service = CreateService();

        // Act
        var results = await service.GetAllUnsent();

        // Assert
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllUnsent_ShouldOrderByDueAtAscending()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, dueAt: baseTime.AddHours(3)),
            ReminderEntityBuilder.Create(id: 2, dueAt: baseTime.AddHours(1)),
            ReminderEntityBuilder.Create(id: 3, dueAt: baseTime.AddHours(2)));

        var service = CreateService();

        // Act
        var results = await service.GetAllUnsent();

        // Assert
        await Assert.That(results[0].Id).IsEqualTo(2); // Earliest first
        await Assert.That(results[1].Id).IsEqualTo(3);
        await Assert.That(results[2].Id).IsEqualTo(1); // Latest last
    }

    [Test]
    public async Task GetAllByUserId_ShouldRetrieveOnlyRemindersForSpecificUser()
    {
        // Arrange
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, userId: 111),
            ReminderEntityBuilder.Create(id: 2, userId: 111),
            ReminderEntityBuilder.Create(id: 3, userId: 222));

        var service = CreateService();

        // Act
        var results = await service.GetAllByUserId(111);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var reminder in results)
        {
            await Assert.That(reminder.UserId).IsEqualTo(111);
        }
    }

    [Test]
    public async Task GetAllByUserId_ShouldExcludeSentReminders()
    {
        // Arrange
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, userId: 111, status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 2, userId: 111, status: ReminderStatus.Sent));

        var service = CreateService();

        // Act
        var results = await service.GetAllByUserId(111);

        // Assert
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(1);
    }

    [Test]
    public async Task GetAllByUserId_ShouldReturnEmptyListForUserWithNoReminders()
    {
        // Arrange
        await InsertRemindersAsync(ReminderEntityBuilder.Create(id: 1, userId: 111));

        var service = CreateService();

        // Act
        var results = await service.GetAllByUserId(999);

        // Assert
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllByUserId_ShouldOrderByDueAtAscending()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, userId: 111, dueAt: baseTime.AddHours(3)),
            ReminderEntityBuilder.Create(id: 2, userId: 111, dueAt: baseTime.AddHours(1)),
            ReminderEntityBuilder.Create(id: 3, userId: 111, dueAt: baseTime.AddHours(2)));

        var service = CreateService();

        // Act
        var results = await service.GetAllByUserId(111);

        // Assert
        await Assert.That(results[0].Id).IsEqualTo(2); // Earliest first
        await Assert.That(results[1].Id).IsEqualTo(3);
        await Assert.That(results[2].Id).IsEqualTo(1); // Latest last
    }

    [Test]
    public async Task DeleteByIdAsync_ShouldRemoveReminderFromDatabase()
    {
        // Arrange
        await InsertRemindersAsync(ReminderEntityBuilder.Create(id: 1));

        var service = CreateService();

        // Act
        var result = await service.DeleteByIdAsync(1);

        // Assert
        await Assert.That(result).IsTrue();
        var reminder = await GetReminderAsync(1);
        await Assert.That(reminder.Status).IsEqualTo(ReminderStatus.Canceled);
    }

    [Test]
    public async Task DeleteByIdAsync_ShouldReturnFalseForNonExistentId()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.DeleteByIdAsync(99999);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task DeleteByIdAsync_ShouldOnlyDeleteSpecifiedReminder()
    {
        // Arrange
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1),
            ReminderEntityBuilder.Create(id: 2),
            ReminderEntityBuilder.Create(id: 3));

        var service = CreateService();

        // Act
        await service.DeleteByIdAsync(2);

        // Assert
        var canceled = await GetReminderAsync(2);
        await Assert.That(canceled.Status).IsEqualTo(ReminderStatus.Canceled);
        var active = await GetActiveRemindersAsync();
        await Assert.That(active.Select(r => r.Id)).Contains(1L);
        await Assert.That(active.Select(r => r.Id)).Contains(3L);
    }

    [Test]
    public async Task ClaimDueBatchAsync_ClaimsPendingRemindersInOrder()
    {
        var now = DateTimeOffset.UtcNow;
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, dueAt: now.AddMinutes(-10), status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 2, dueAt: now.AddMinutes(-5), status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 3, dueAt: now.AddMinutes(5), status: ReminderStatus.Pending));

        var service = CreateService();

        var claimed = await service.ClaimDueBatchAsync(10, now);

        await Assert.That(claimed.Select(r => r.Id).ToArray()).IsEquivalentTo([1L, 2L]);

        var claimedEntities = await GetRemindersAsync(1, 2);
        await Assert.That(claimedEntities.All(r => r.Status == ReminderStatus.Processing)).IsTrue();
        await Assert.That(claimedEntities.All(r => r.AttemptCount == 1)).IsTrue();
    }

    [Test]
    public async Task MarkSentAsync_TransitionsProcessingRemindersToSent()
    {
        await InsertRemindersAsync(ReminderEntityBuilder.Create(id: 1, status: ReminderStatus.Processing, attemptCount: 1));

        var service = CreateService();
        var sentAt = DateTimeOffset.UtcNow;

        var updated = await service.MarkSentAsync([1L], sentAt);

        await Assert.That(updated).IsEqualTo(1);
        var reminder = await GetReminderAsync(1);
        await Assert.That(reminder.Status).IsEqualTo(ReminderStatus.Sent);
        await Assert.That(reminder.SentAt).IsNotNull();
        await Assert.That(PostgresTimestamp.Normalize(reminder.SentAt)).IsEqualTo(PostgresTimestamp.Normalize(sentAt));
    }

    [Test]
    public async Task MarkFailedAndRequeue_UpdateReminderState()
    {
        await InsertRemindersAsync(ReminderEntityBuilder.Create(id: 1, status: ReminderStatus.Processing, attemptCount: 2));

        var service = CreateService();

        var markFailed = await service.MarkFailedAsync(1, "discord exploded");
        await Assert.That(markFailed).IsTrue();

        var nextDue = DateTimeOffset.UtcNow.AddMinutes(10);
        var requeued = await service.RequeueAsync(1, nextDue, "retrying");
        await Assert.That(requeued).IsTrue();

        var reminder = await GetReminderAsync(1);
        await Assert.That(reminder.Status).IsEqualTo(ReminderStatus.Pending);
        await Assert.That(PostgresTimestamp.Normalize(reminder.DueAt)).IsEqualTo(PostgresTimestamp.Normalize(nextDue));
        await Assert.That(reminder.LastError).IsEqualTo("retrying");
    }

    [Test]
    public async Task GetNextDueTimeAsync_ReturnsEarliestPendingReminder()
    {
        var now = DateTimeOffset.UtcNow;
        await InsertRemindersAsync(
            ReminderEntityBuilder.Create(id: 1, dueAt: now.AddHours(3), status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 2, dueAt: now.AddHours(1), status: ReminderStatus.Pending),
            ReminderEntityBuilder.Create(id: 3, dueAt: now.AddMinutes(30), status: ReminderStatus.Canceled));

        var service = CreateService();

        var nextDue = await service.GetNextDueTimeAsync();

        await Assert.That(nextDue).IsNotNull();
        await Assert.That(PostgresTimestamp.Normalize(nextDue)).IsEqualTo(PostgresTimestamp.Normalize(now.AddHours(1)));
    }
}
