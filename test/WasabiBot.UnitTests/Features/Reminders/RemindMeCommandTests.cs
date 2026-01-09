using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Reminders;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Reminders;

public class RemindMeCommandTests
{
    private static RemindMeCommand CreateCommand(
        IReminderService reminderService,
        ITimeParsingService timeParsingService,
        TimeProvider timeProvider)
    {
        return new RemindMeCommand(
            NullLogger<RemindMeCommand>.Instance,
            reminderService,
            timeParsingService,
            timeProvider);
    }

    [Test]
    public async Task ExecuteAsync_WhenScheduleSucceeds_RespondsWithConfirmation()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .ScheduleAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns(Task.FromResult(true));

        var parsedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero).AddHours(1);
        var timeParsingService = Substitute.For<ITimeParsingService>();
        timeParsingService
            .ParseTimeAsync("in 45 minutes")
            .Returns(Task.FromResult<DateTimeOffset?>(parsedTime));

        var now = parsedTime.AddMinutes(-5);
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);

        var command = CreateCommand(reminderService, timeParsingService, timeProvider);
        var context = new FakeCommandContext(userId: 42, channelId: 9001, userDisplayName: "ReminderUser");

        await command.ExecuteAsync(context, "  in 45 minutes  ", "  stretch   ");

        await timeParsingService.Received(1).ParseTimeAsync("in 45 minutes");
        await reminderService.Received(1).ScheduleAsync(42UL, 9001UL, "stretch", parsedTime);

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        var expectedTimestamp = parsedTime.ToUnixTimeSeconds();
        await Assert.That(message)
            .IsEqualTo($"I'll remind you <t:{expectedTimestamp}:f>: stretch");
    }

    [Test]
    public async Task ExecuteAsync_WhenTimeParsingThrows_SendsFriendlyError()
    {
        var reminderService = Substitute.For<IReminderService>();
        var timeParsingService = Substitute.For<ITimeParsingService>();
        timeParsingService
            .ParseTimeAsync(Arg.Any<string>())
            .Returns(Task.FromException<DateTimeOffset?>(new InvalidOperationException("boom")));

        var timeProvider = Substitute.For<TimeProvider>();
        var command = CreateCommand(reminderService, timeParsingService, timeProvider);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "soon", "hydrate");

        await reminderService.DidNotReceive().ScheduleAsync(
            Arg.Any<ulong>(),
            Arg.Any<ulong>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>());

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single())
            .IsEqualTo("Sorry, I couldn't understand that time. Try phrases like 'in 30 minutes' or 'tomorrow at 9am'.");
    }

    [Test]
    public async Task ExecuteAsync_WhenTimeParsingReturnsNull_SendsFriendlyError()
    {
        var reminderService = Substitute.For<IReminderService>();
        var timeParsingService = Substitute.For<ITimeParsingService>();
        timeParsingService
            .ParseTimeAsync(Arg.Any<string>())
            .Returns(Task.FromResult<DateTimeOffset?>(null));

        var timeProvider = Substitute.For<TimeProvider>();
        var command = CreateCommand(reminderService, timeParsingService, timeProvider);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "in 5 minutes", "refill water");

        await reminderService.DidNotReceive().ScheduleAsync(
            Arg.Any<ulong>(),
            Arg.Any<ulong>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>());

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single())
            .IsEqualTo("Sorry, I couldn't understand that time. Try phrases like 'in 30 minutes' or 'tomorrow at 9am'.");
    }

    [Test]
    public async Task ExecuteAsync_WhenParsedTimeIsNotInFuture_SendsFutureOnlyWarning()
    {
        var reminderService = Substitute.For<IReminderService>();

        var parsedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var timeParsingService = Substitute.For<ITimeParsingService>();
        timeParsingService
            .ParseTimeAsync(Arg.Any<string>())
            .Returns(Task.FromResult<DateTimeOffset?>(parsedTime));

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(parsedTime);

        var command = CreateCommand(reminderService, timeParsingService, timeProvider);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "yesterday", "ping friend");

        await reminderService.DidNotReceive().ScheduleAsync(
            Arg.Any<ulong>(),
            Arg.Any<ulong>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>());

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("Please choose a time in the future.");
    }

    [Test]
    public async Task ExecuteAsync_WhenSchedulingFails_NotifiesUser()
    {
        var reminderService = Substitute.For<IReminderService>();
        reminderService
            .ScheduleAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns(Task.FromResult(false));

        var parsedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero).AddHours(2);
        var timeParsingService = Substitute.For<ITimeParsingService>();
        timeParsingService
            .ParseTimeAsync(Arg.Any<string>())
            .Returns(Task.FromResult<DateTimeOffset?>(parsedTime));

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(parsedTime.AddMinutes(-10));

        var command = CreateCommand(reminderService, timeParsingService, timeProvider);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "in 10 minutes", "take break");

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("I couldn't schedule that reminder. Please try again.");
    }
}
