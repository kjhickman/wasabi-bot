using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Infrastructure.Database.Entities;

namespace WasabiBot.UnitTests.Features.Reminders;

public class ReminderDispatcherTests
{
    private static ReminderEntity CreateReminder(long id, DateTimeOffset dueAt) => new()
    {
        Id = id,
        UserId = 42,
        ChannelId = 9001,
        ReminderMessage = $"test-{id}",
        DueAt = dueAt,
        CreatedAt = dueAt.AddMinutes(-5),
        Status = ReminderStatus.Processing,
        AttemptCount = 1
    };

    private static ServiceProvider BuildProvider(IReminderService reminderService, Tracer tracer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reminderService);
        services.AddSingleton(tracer);
        return services.BuildServiceProvider();
    }

    [Test]
    public async Task ExecuteAsync_WhenNoPendingReminders_WaitsForWakeSignal()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => DateTimeOffset.UtcNow);

        var wakeSignal = Substitute.For<IReminderWakeSignal>();
        var waitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        wakeSignal.WaitAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            waitObserved.TrySetResult(true);
            var token = call.ArgAt<CancellationToken>(0);
            return Task.Delay(Timeout.Infinite, token);
        });

        var reminderService = Substitute.For<IReminderService>();
        reminderService.ClaimDueBatchAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        reminderService.GetNextDueTimeAsync(Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);

        var tracer = TracerProvider.Default.GetTracer("reminder-tests");
        await using var provider = BuildProvider(reminderService, tracer);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var dispatcher = new ReminderDispatcher(NullLogger<ReminderDispatcher>.Instance, scopeFactory, wakeSignal, timeProvider);

        await dispatcher.StartAsync(CancellationToken.None);
        await waitObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await dispatcher.StopAsync(CancellationToken.None);

        try
        {
            await wakeSignal.Received(1).WaitAsync(Arg.Any<CancellationToken>());
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenDueRemindersExist_SendsClaimedBatch()
    {
        var now = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);

        var reminder = CreateReminder(10, now.AddMinutes(-1));
        var reminderService = Substitute.For<IReminderService>();
        reminderService.ClaimDueBatchAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var token = ci.ArgAt<CancellationToken>(2);
                return token.IsCancellationRequested ? [] : [reminder];
            });

        var sendObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        reminderService.SendRemindersAsync(Arg.Any<IEnumerable<ReminderEntity>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var batch = call.ArgAt<IEnumerable<ReminderEntity>>(0).ToArray();
                if (batch.Length == 1 && batch[0].Id == reminder.Id)
                {
                    sendObserved.TrySetResult(true);
                }

                IReadOnlyCollection<long> ids = [reminder.Id];
                return Task.FromResult(ids);
            });

        var wakeSignal = Substitute.For<IReminderWakeSignal>();

        var tracer = TracerProvider.Default.GetTracer("reminder-tests");
        await using var provider = BuildProvider(reminderService, tracer);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var dispatcher = new ReminderDispatcher(NullLogger<ReminderDispatcher>.Instance, scopeFactory, wakeSignal, timeProvider);

        await dispatcher.StartAsync(CancellationToken.None);
        await sendObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await dispatcher.StopAsync(CancellationToken.None);

        await reminderService.Received().SendRemindersAsync(
            Arg.Is<IEnumerable<ReminderEntity>>(batch => batch.Single().Id == reminder.Id),
            Arg.Any<CancellationToken>());
    }
}
