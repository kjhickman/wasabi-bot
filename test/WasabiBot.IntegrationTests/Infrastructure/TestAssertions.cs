using Microsoft.EntityFrameworkCore;
using TUnit.Core;
using WasabiBot.DataAccess;

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
}
