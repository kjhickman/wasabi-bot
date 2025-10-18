using NSubstitute;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.UnitTests;

public class InMemoryReminderStoreTests
{
    private static ReminderEntity CreateReminder(long id, DateTimeOffset remindAt, bool isSent = false)
        => new()
        {
            Id = id,
            UserId = 123,
            ChannelId = 456,
            ReminderMessage = $"reminder-{id}",
            RemindAt = remindAt,
            CreatedAt = remindAt.AddMinutes(-1),
            IsReminderSent = isSent,
        };

    [Test]
    public async Task GetNextDueTime_WhenEmpty_ReturnsNull()
    {
        var currentTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        await Assert.That(store.GetNextDueTime()).IsNull();
    }

    [Test]
    public async Task Insert_IgnoresAlreadySentReminders()
    {
        var baseTime = new DateTimeOffset(2025, 2, 3, 10, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        var sentReminder = CreateReminder(1, baseTime.AddMinutes(-5), isSent: true);

        store.Insert(sentReminder);
        currentTime = baseTime.AddHours(1);

        await Assert.That(store.GetNextDueTime()).IsNull();
        await Assert.That(store.GetAllDueReminders().Count).IsEqualTo(0);
    }

    [Test]
    public async Task InsertMany_AddsUnsentRemindersInDueOrder()
    {
        var baseTime = new DateTimeOffset(2025, 3, 4, 12, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waitTask = store.WaitForEarlierAsync(cts.Token);

        store.InsertMany(
        [
            CreateReminder(10, baseTime.AddMinutes(3)),
            CreateReminder(11, baseTime.AddMinutes(1)),
            CreateReminder(12, baseTime.AddMinutes(2)),
            CreateReminder(13, baseTime.AddMinutes(1), isSent: true),
        ]);

        await waitTask;

        await Assert.That(store.GetNextDueTime()).IsEqualTo(baseTime.AddMinutes(1));

        currentTime = baseTime.AddHours(1);

        var dueIds = store.GetAllDueReminders().Select(r => r.Id).ToArray();
        await Assert.That(dueIds.SequenceEqual(new long[] { 11, 12, 10 })).IsTrue();
    }

    [Test]
    public async Task GetAllDueReminders_StopsAtFirstFuture()
    {
        var baseTime = new DateTimeOffset(2025, 4, 5, 8, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        store.Insert(CreateReminder(1, baseTime.AddMinutes(-10)));
        store.Insert(CreateReminder(2, baseTime.AddMinutes(-1)));
        store.Insert(CreateReminder(3, baseTime.AddMinutes(1)));
        store.Insert(CreateReminder(4, baseTime.AddMinutes(2)));

        currentTime = baseTime;

        var dueIds = store.GetAllDueReminders().Select(r => r.Id).ToArray();
        await Assert.That(dueIds.SequenceEqual(new long[] { 1, 2 })).IsTrue();
    }

    [Test]
    public async Task RemoveById_RemovesReminderAndSignalsWhenEarliestChanges()
    {
        var baseTime = new DateTimeOffset(2025, 5, 6, 9, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        var first = CreateReminder(1, baseTime.AddMinutes(1));
        var second = CreateReminder(2, baseTime.AddMinutes(3));

        store.Insert(first);
        store.Insert(second);

        await store.WaitForEarlierAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waitTask = store.WaitForEarlierAsync(cts.Token);

        store.RemoveById(first.Id);

        await waitTask;
        await Assert.That(store.GetNextDueTime()).IsEqualTo(second.RemindAt);
    }

    [Test]
    public async Task WaitForEarlierAsync_CompletesWhenEarlierReminderInserted()
    {
        var baseTime = new DateTimeOffset(2025, 6, 7, 14, 0, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_ => currentTime);
        var store = new InMemoryReminderStore(timeProvider);

        var initial = CreateReminder(1, baseTime.AddMinutes(10));
        store.Insert(initial);
        await store.WaitForEarlierAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waitTask = store.WaitForEarlierAsync(cts.Token);

        var earlier = CreateReminder(2, baseTime.AddMinutes(5));
        store.Insert(earlier);

        await waitTask;
        await Assert.That(store.GetNextDueTime()).IsEqualTo(earlier.RemindAt);
    }
}
