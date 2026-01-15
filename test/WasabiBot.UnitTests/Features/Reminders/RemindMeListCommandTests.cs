using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Reminders;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.DataAccess.Entities;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Reminders;

public class RemindMeListCommandTests
{
    private static RemindMeListCommand CreateCommand(IReminderService reminderService)
    {
        return new RemindMeListCommand(
            NullLogger<RemindMeListCommand>.Instance,
            reminderService);
    }

    [Test]
    public async Task ExecuteAsync_WhenUserHasNoReminders_RespondsWithEmptyMessage()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns([]);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        await reminderService.Received(1).GetAllByUserId(42);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("You have no scheduled reminders.");
    }

    [Test]
    public async Task ExecuteAsync_WhenUserHasReminders_RespondsWithFormattedList()
    {
        var reminders = new List<ReminderEntity>
        {
            new()
            {
                Id = 1,
                UserId = 42,
                ChannelId = 1001,
                ReminderMessage = "Buy groceries",
                RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
                CreatedAt = DateTimeOffset.UtcNow,
                IsReminderSent = false
            },
            new()
            {
                Id = 2,
                UserId = 42,
                ChannelId = 1002,
                ReminderMessage = "Call mom",
                RemindAt = new DateTimeOffset(2024, 1, 16, 10, 0, 0, TimeSpan.Zero),
                CreatedAt = DateTimeOffset.UtcNow,
                IsReminderSent = false
            }
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns(reminders);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        
        var message = ephemerals.Single();
        await Assert.That(message).Contains("Your Scheduled Reminders:");
        await Assert.That(message).Contains("ID 1");
        await Assert.That(message).Contains("ID 2");
        await Assert.That(message).Contains("Buy groceries");
        await Assert.That(message).Contains("Call mom");
        await Assert.That(message).Contains("<#1001>");
        await Assert.That(message).Contains("<#1002>");
        await Assert.That(message).Contains("Total: 2 reminder(s)");
    }

    [Test]
    public async Task ExecuteAsync_TruncatesLongReminderMessages()
    {
        var longMessage = new string('a', 100);
        var reminders = new List<ReminderEntity>
        {
            new()
            {
                Id = 1,
                UserId = 42,
                ChannelId = 1001,
                ReminderMessage = longMessage,
                RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
                CreatedAt = DateTimeOffset.UtcNow,
                IsReminderSent = false
            }
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns(reminders);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        // Message should contain truncated version (max 80 chars + "...")
        await Assert.That(message).Contains(new string('a', 77) + "...");
        await Assert.That(!message.Contains(longMessage)).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_DoesNotTruncateShortMessages()
    {
        var shortMessage = "Short reminder";
        var reminders = new List<ReminderEntity>
        {
            new()
            {
                Id = 1,
                UserId = 42,
                ChannelId = 1001,
                ReminderMessage = shortMessage,
                RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
                CreatedAt = DateTimeOffset.UtcNow,
                IsReminderSent = false
            }
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns(reminders);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains(shortMessage);
    }

    [Test]
    public async Task ExecuteAsync_ResponseIsEphemeral()
    {
        var reminders = new List<ReminderEntity>
        {
            new()
            {
                Id = 1,
                UserId = 42,
                ChannelId = 1001,
                ReminderMessage = "Test reminder",
                RemindAt = new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero),
                CreatedAt = DateTimeOffset.UtcNow,
                IsReminderSent = false
            }
        };

        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns(reminders);

        var command = CreateCommand(reminderService);
        var context = new FakeCommandContext(userId: 42);

        await command.ExecuteAsync(context);

        var (_, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_FetchesRemindersForCorrectUser()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .GetAllByUserId(Arg.Any<long>())
            .Returns([]);

        var command = CreateCommand(reminderService);
        var userId = 999UL;
        var context = new FakeCommandContext(userId: userId);

        await command.ExecuteAsync(context);

        await reminderService.Received(1).GetAllByUserId(999);
    }
}
