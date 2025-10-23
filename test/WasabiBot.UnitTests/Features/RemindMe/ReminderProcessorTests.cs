using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.UnitTests.Features.RemindMe;

public class ReminderProcessorTests
{
    private static ReminderEntity CreateReminder(long id, DateTimeOffset remindAt) => new()
    {
        Id = id,
        UserId = 42,
        ChannelId = 9001,
        ReminderMessage = $"test-{id}",
        RemindAt = remindAt,
        CreatedAt = remindAt.AddMinutes(-5),
        IsReminderSent = false,
    };

    private static ServiceProvider BuildProvider(IReminderService reminderService, Tracer tracer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reminderService);
        services.AddSingleton(tracer);
        return services.BuildServiceProvider();
    }

    [Test]
    public async Task ExecuteAsync_LoadsAndProcessesDueReminders()
    {
        var baseTime = new DateTimeOffset(2025, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);

        var store = new InMemoryReminderStore(timeProvider);
        var reminderService = Substitute.For<IReminderService>();
        var dueReminder = CreateReminder(100, baseTime.AddMinutes(-2));
        reminderService.GetAllUnsent(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ReminderEntity> { dueReminder }));

        List<ReminderEntity>? sentBatch = null;
        var sendInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        reminderService.SendRemindersAsync(Arg.Any<IEnumerable<ReminderEntity>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sentBatch = call.ArgAt<IEnumerable<ReminderEntity>>(0).ToList();
                sendInvoked.TrySetResult(true);
                IReadOnlyCollection<long> sentIds = new[] { dueReminder.Id };
                return Task.FromResult(sentIds);
            });

        var tracer = TracerProvider.Default.GetTracer("reminder-tests");
        await using var provider = BuildProvider(reminderService, tracer);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var processor = new ReminderProcessor(NullLogger<ReminderProcessor>.Instance, scopeFactory, store, timeProvider);

        await processor.StartAsync(CancellationToken.None);
        await sendInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await processor.StopAsync(CancellationToken.None);

        await Assert.That(sentBatch).IsNotNull();
        await Assert.That(sentBatch!.Select(r => r.Id).ToArray()).IsEquivalentTo(new long[] { dueReminder.Id });
        await Assert.That(store.GetNextDueTime()).IsNull();

        await reminderService.Received(1).GetAllUnsent(Arg.Any<CancellationToken>());
        await reminderService.Received(1).SendRemindersAsync(Arg.Any<IEnumerable<ReminderEntity>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WaitsForEarlierWhenQueueEmpty()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => DateTimeOffset.UtcNow);

        var store = Substitute.For<IReminderStore>();
        store.GetNextDueTime().Returns((DateTimeOffset?)null);
        store.GetAllDueReminders().Returns(new List<ReminderEntity>());

        var waitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.WaitForEarlierAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            waitObserved.TrySetResult(true);
            var token = call.ArgAt<CancellationToken>(0);
            return Task.Delay(Timeout.Infinite, token);
        });

        var reminderService = Substitute.For<IReminderService>();
        reminderService.GetAllUnsent(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ReminderEntity>()));

        var tracer = TracerProvider.Default.GetTracer("reminder-tests");
        await using var provider = BuildProvider(reminderService, tracer);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var processor = new ReminderProcessor(NullLogger<ReminderProcessor>.Instance, scopeFactory, store, timeProvider);

        await processor.StartAsync(CancellationToken.None);
        await waitObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await processor.StopAsync(CancellationToken.None);

        try
        {
            await store.Received(1).WaitForEarlierAsync(Arg.Any<CancellationToken>());
        }
        catch (OperationCanceledException)
        {
            // cancellation from StopAsync is expected
        }

        await reminderService.Received(1).GetAllUnsent(Arg.Any<CancellationToken>());
    }
}
