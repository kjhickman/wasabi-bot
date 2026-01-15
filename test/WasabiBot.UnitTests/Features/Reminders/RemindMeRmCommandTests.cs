using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Reminders;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.DataAccess.Entities;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Reminders;

public class RemindMeRmCommandTests
{
    private static RemindMeRmCommand CreateCommand(IReminderService reminderService)
    {
        return new RemindMeRmCommand(
            NullLogger<RemindMeRmCommand>.Instance,
            reminderService);
    }

    [Test]
    public async Task ExecuteAsync_WhenReminderDoesNotExist_RespondsWithNotFoundError()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns((ReminderEntity?)null);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 999);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("❌ Reminder `999` does not exist.");
    }

    [Test]
    public async Task ExecuteAsync_WhenReminderBelongsToAnotherUser_RespondsWithPermissionError()
    {
        var reminder = new ReminderEntity
        {
            Id = 1,
            UserId = 999, // Different user
            ChannelId = 1001,
            ReminderMessage = "Someone else's reminder",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42); // Different user

        await command.ExecuteAsync(context, 1);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("❌ You may only delete your own reminders.");
    }

    [Test]
    public async Task ExecuteAsync_WhenDeletionSucceeds_RespondsWithSuccessMessage()
    {
        var reminder = new ReminderEntity
        {
            Id = 1,
            UserId = 42,
            ChannelId = 1001,
            ReminderMessage = "Buy groceries",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);
        reminderService
            .DeleteByIdAsync(Arg.Any<long>())
            .Returns(true);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 1);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("✅ Reminder `1` has been deleted.");
    }

    [Test]
    public async Task ExecuteAsync_WhenDeletionFails_RespondsWithFailureMessage()
    {
        var reminder = new ReminderEntity
        {
            Id = 1,
            UserId = 42,
            ChannelId = 1001,
            ReminderMessage = "Buy groceries",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);
        reminderService
            .DeleteByIdAsync(Arg.Any<long>())
            .Returns(false);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 1);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("❌ Failed to delete reminder `1`. Please try again.");
    }

    [Test]
    public async Task ExecuteAsync_CallsDeleteWithCorrectReminderId()
    {
        var reminder = new ReminderEntity
        {
            Id = 123,
            UserId = 42,
            ChannelId = 1001,
            ReminderMessage = "Test",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);
        reminderService
            .DeleteByIdAsync(Arg.Any<long>())
            .Returns(true);
        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 123);

        await reminderService.Received(1).DeleteByIdAsync(123);
    }

    [Test]
    public async Task ExecuteAsync_ResponseIsEphemeral()
    {
        var reminder = new ReminderEntity
        {
            Id = 1,
            UserId = 42,
            ChannelId = 1001,
            ReminderMessage = "Test",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);
        reminderService
            .DeleteByIdAsync(Arg.Any<long>())
            .Returns(true);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 1);

        var messages = context.Messages;
        await Assert.That(messages.Count).IsGreaterThanOrEqualTo(1);
        var (_, ephemeral) = messages[0];
        await Assert.That(ephemeral).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_FetchesReminderWithCorrectId()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns((ReminderEntity?)null);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 456);

        await reminderService.Received(1).GetByIdAsync(456);
    }

    [Test]
    public async Task ExecuteAsync_WhenUserIsNotReminder_OwnerDoesNotAttemptDelete()
    {
        var reminder = new ReminderEntity
        {
            Id = 1,
            UserId = 999, // Different user
            ChannelId = 1001,
            ReminderMessage = "Someone else's reminder",
            RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            IsReminderSent = false
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetByIdAsync(Arg.Any<long>())
            .Returns(reminder);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context, 1);

        // Delete should not be called
        await reminderService.DidNotReceive().DeleteByIdAsync(Arg.Any<long>());
    }
}
