using Microsoft.EntityFrameworkCore;
using TUnit.Core;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.IntegrationTests.Infrastructure;

/// <summary>
/// Provides reusable assertion helpers for integration tests.
/// </summary>
public static class TestAssertions
{
    /// <summary>Asserts that an interaction exists in the database.</summary>
    public static async Task AssertInteractionExistsAsync(WasabiBotContext context, long interactionId)
    {
        var interaction = await context.Interactions.FirstOrDefaultAsync(i => i.Id == interactionId);
        await Assert.That(interaction).IsNotNull();
    }

    /// <summary>Counts total interactions in the database.</summary>
    public static async Task<int> CountInteractionsAsync(WasabiBotContext context)
    {
        return await context.Interactions.CountAsync();
    }

    /// <summary>Asserts that a reminder exists for the given user and channel.</summary>
    public static async Task AssertReminderExistsAsync(WasabiBotContext context, long userId, long channelId)
    {
        var reminder = await context.Reminders.FirstOrDefaultAsync(r => r.UserId == userId && r.ChannelId == channelId);
        await Assert.That(reminder).IsNotNull();
    }

    /// <summary>Gets the first reminder in the database.</summary>
    public static async Task<ReminderEntity?> GetFirstReminderAsync(WasabiBotContext context)
    {
        return await context.Reminders.FirstOrDefaultAsync();
    }

    /// <summary>Counts total reminders in the database.</summary>
    public static async Task<int> CountRemindersAsync(WasabiBotContext context)
    {
        return await context.Reminders.CountAsync();
    }
}
