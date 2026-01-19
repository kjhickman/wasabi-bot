using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using TUnit.Core;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.DataAccess.Entities;
using WasabiBot.IntegrationTests.Infrastructure;
using WasabiBot.TestShared.Builders;

namespace WasabiBot.IntegrationTests.Features.Reminders;

/// <summary>
/// Integration tests for ReminderService.
/// Tests CRUD operations against a real PostgreSQL database.
/// Note: RestClient is sealed and cannot be mocked, so we pass null.
/// Tests that require Discord functionality should be in a separate test class.
/// </summary>
public class ReminderServiceTests : IntegrationTestBase
{
    private IReminderStore CreateMockStore() => Substitute.For<IReminderStore>();

    private ReminderService CreateService(IReminderStore? store = null)
    {
        var context = CreateContext();
        return new ReminderService(
            context,
            store ?? CreateMockStore(),
            null!, // RestClient is sealed and not needed for database tests
            NullLogger<ReminderService>.Instance,
            TracerProvider.Default.GetTracer("test"),
            TimeProvider.System);
    }

    [Test]
    public async Task ScheduleAsync_ShouldInsertReminderIntoDatabase()
    {
        // Arrange
        var store = CreateMockStore();
        var service = CreateService(store: store);
        var remindAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var result = await service.ScheduleAsync(123, 456, "Test reminder", remindAt);

        // Assert
        await Assert.That(result).IsTrue();
        await using var assertContext = CreateContext();
        await TestAssertions.AssertReminderExistsAsync(assertContext, 123, 456);
        store.Received(1).Insert(Arg.Any<ReminderEntity>());
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
        await using var assertContext = CreateContext();
        var reminder = await TestAssertions.GetFirstReminderAsync(assertContext);
        await Assert.That(reminder).IsNotNull();
        await Assert.That(reminder!.UserId).IsEqualTo(111);
        await Assert.That(reminder.ChannelId).IsEqualTo(222);
        await Assert.That(reminder.ReminderMessage).IsEqualTo(message);
        await Assert.That(reminder.IsReminderSent).IsFalse();
    }

    [Test]
    public async Task GetByIdAsync_ShouldRetrieveSingleReminderById()
    {
        // Arrange
        var service = CreateService();
        var remindAt = DateTimeOffset.UtcNow.AddHours(1);
        await service.ScheduleAsync(123, 456, "Test reminder", remindAt);

        await using var assertContext = CreateContext();
        var createdReminder = await TestAssertions.GetFirstReminderAsync(assertContext);

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
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, isReminderSent: false));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, isReminderSent: false));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 3, isReminderSent: true));
        await context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var results = await service.GetAllUnsent();

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var reminder in results)
        {
            await Assert.That(reminder.IsReminderSent).IsFalse();
        }
    }

    [Test]
    public async Task GetAllUnsent_ShouldReturnEmptyListWhenNoUnsentReminders()
    {
        // Arrange
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, isReminderSent: true));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, isReminderSent: true));
        await context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var results = await service.GetAllUnsent();

        // Assert
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllUnsent_ShouldOrderByRemindAtAscending()
    {
        // Arrange
        await using var context = CreateContext();
        var baseTime = DateTimeOffset.UtcNow;
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, remindAt: baseTime.AddHours(3)));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, remindAt: baseTime.AddHours(1)));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 3, remindAt: baseTime.AddHours(2)));
        await context.SaveChangesAsync();

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
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, userId: 111));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, userId: 111));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 3, userId: 222));
        await context.SaveChangesAsync();

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
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, userId: 111, isReminderSent: false));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, userId: 111, isReminderSent: true));
        await context.SaveChangesAsync();

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
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, userId: 111));
        await context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var results = await service.GetAllByUserId(999);

        // Assert
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllByUserId_ShouldOrderByRemindAtAscending()
    {
        // Arrange
        await using var context = CreateContext();
        var baseTime = DateTimeOffset.UtcNow;
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1, userId: 111, remindAt: baseTime.AddHours(3)));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2, userId: 111, remindAt: baseTime.AddHours(1)));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 3, userId: 111, remindAt: baseTime.AddHours(2)));
        await context.SaveChangesAsync();

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
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1));
        await context.SaveChangesAsync();

        var store = CreateMockStore();
        var service = CreateService(store: store);

        // Act
        var result = await service.DeleteByIdAsync(1);

        // Assert
        await Assert.That(result).IsTrue();
        await using var assertContext = CreateContext();
        var count = await TestAssertions.CountRemindersAsync(assertContext);
        await Assert.That(count).IsEqualTo(0);
        store.Received(1).RemoveById(1);
    }

    [Test]
    public async Task DeleteByIdAsync_ShouldReturnFalseForNonExistentId()
    {
        // Arrange
        var store = CreateMockStore();
        var service = CreateService(store: store);

        // Act
        var result = await service.DeleteByIdAsync(99999);

        // Assert
        await Assert.That(result).IsFalse();
        store.DidNotReceive().RemoveById(Arg.Any<long>());
    }

    [Test]
    public async Task DeleteByIdAsync_ShouldOnlyDeleteSpecifiedReminder()
    {
        // Arrange
        await using var context = CreateContext();
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 1));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 2));
        context.Reminders.Add(ReminderEntityBuilder.Create(id: 3));
        await context.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.DeleteByIdAsync(2);

        // Assert
        await using var assertContext = CreateContext();
        var count = await TestAssertions.CountRemindersAsync(assertContext);
        await Assert.That(count).IsEqualTo(2);

        var remaining = await assertContext.Reminders.ToListAsync();
        await Assert.That(remaining.Select(r => r.Id)).Contains(1L);
        await Assert.That(remaining.Select(r => r.Id)).Contains(3L);
        await Assert.That(remaining.Select(r => r.Id)).DoesNotContain(2L);
    }
}
