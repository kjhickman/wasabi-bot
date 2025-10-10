using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;
using Xunit;
using WasabiBot.IntegrationTests.Assertions;

namespace WasabiBot.IntegrationTests.DataAccess;

[Collection(nameof(TestFixture))]
public class ReminderServiceTests : IAsyncDisposable
{
    private readonly TestFixture _fixture;

    public ReminderServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<List<ReminderEntity>> GetAllAsync(WasabiBotContext ctx)
        => await ctx.Reminders.AsNoTracking().OrderBy(r => r.Id).ToListAsync();

    [Fact]
    public async Task CreateAsync_Valid_InsertsRow()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

        var remindAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var created = await service.CreateAsync(1, 10, "Test reminder", remindAt);
        Assert.True(created);

        var all = await GetAllAsync(ctx);
        var reminder = Assert.Single(all);
        Assert.Equal(1, reminder.UserId);
        Assert.Equal(10, reminder.ChannelId);
        Assert.Equal("Test reminder", reminder.ReminderMessage);
        TimeAssert.WithinPostgresPrecision(remindAt, reminder.RemindAt);
        Assert.False(reminder.IsReminderSent);
    }

    [Fact]
    public async Task GetDueAsync_ReturnsOnlyDueAndUnsent_Ordered()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

        var now = DateTimeOffset.UtcNow;
        // Due reminders
        await service.CreateAsync(1, 10, "due1", now.AddMinutes(-10));
        await service.CreateAsync(1, 10, "due2", now.AddMinutes(-5));
        await service.CreateAsync(1, 10, "due3", now); // exactly now
        // Future reminder
        await service.CreateAsync(1, 10, "future", now.AddMinutes(5));

        // Sent (should not appear)
        var sent = new ReminderEntity
        {
            UserId = 1,
            ChannelId = 10,
            ReminderMessage = "alreadySent",
            RemindAt = now.AddMinutes(-1),
            CreatedAt = now.AddMinutes(-2),
            IsReminderSent = true
        };
        ctx.Reminders.Add(sent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await service.GetDueAsync(now, TestContext.Current.CancellationToken);
        Assert.Equal(3, due.Count); // due1, due2, due3
        Assert.True(due.SequenceEqual(due.OrderBy(r => r.RemindAt))); // ordered ascending
        Assert.DoesNotContain(due, r => r.ReminderMessage is "future" or "alreadySent");
    }

    [Fact]
    public async Task MarkSentAsync_UpdatesSpecifiedIdsOnly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

        var now = DateTimeOffset.UtcNow;
        await service.CreateAsync(1, 10, "r1", now.AddMinutes(-1));
        await service.CreateAsync(1, 10, "r2", now.AddMinutes(-2));
        await service.CreateAsync(1, 10, "r3", now.AddMinutes(-3));

        var allBefore = await ctx.Reminders.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var ids = allBefore.OrderBy(r => r.ReminderMessage).Select(r => r.Id).ToArray();
        var markIds = ids.Take(2).ToArray();

        await service.MarkSentAsync(markIds, TestContext.Current.CancellationToken);

        var allAfter = await ctx.Reminders.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        foreach (var r in allAfter)
        {
            if (markIds.Contains(r.Id)) Assert.True(r.IsReminderSent); else Assert.False(r.IsReminderSent);
        }
    }

    [Fact]
    public async Task MarkSentAsync_Empty_NoChanges()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

        var now = DateTimeOffset.UtcNow;
        await service.CreateAsync(1, 10, "r1", now.AddMinutes(-1));
        var before = await ctx.Reminders.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);

        await service.MarkSentAsync([], TestContext.Current.CancellationToken);
        var after = await ctx.Reminders.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(before.IsReminderSent);
        Assert.False(after.IsReminderSent);
    }

    [Fact]
    public async Task MarkSentAsync_Idempotent_WhenAlreadySent()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

        var now = DateTimeOffset.UtcNow;
        await service.CreateAsync(1, 10, "r1", now.AddMinutes(-1));
        var reminder = await ctx.Reminders.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        await service.MarkSentAsync([reminder.Id], TestContext.Current.CancellationToken);
        // second call should not fail
        await service.MarkSentAsync([reminder.Id], TestContext.Current.CancellationToken);
        var after = await ctx.Reminders.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(after.IsReminderSent);
    }

    [Fact]
    public async Task GetDueAsync_AfterMarkSent_ExcludesSent()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
        var now = DateTimeOffset.UtcNow;
        await service.CreateAsync(1, 10, "r1", now.AddMinutes(-1));
        await service.CreateAsync(1, 10, "r2", now.AddMinutes(-2));

        var dueBefore = await service.GetDueAsync(now, TestContext.Current.CancellationToken);
        Assert.Equal(2, dueBefore.Count);

        await service.MarkSentAsync(dueBefore.Select(r => r.Id), TestContext.Current.CancellationToken);
        var dueAfter = await service.GetDueAsync(now, TestContext.Current.CancellationToken);
        Assert.Empty(dueAfter);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.ResetDatabase();
    }
}
