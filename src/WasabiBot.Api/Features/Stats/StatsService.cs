using System.Text.Json;
using DictionaryEntry;
using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess;

namespace WasabiBot.Api.Features.Stats;

public interface IStatsService
{
    Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default);
}

public class StatsService : IStatsService
{
    private readonly WasabiBotContext _context;

    public StatsService(WasabiBotContext context)
    {
        _context = context;
    }

    public async Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default)
    {
        var query = _context.Interactions.AsQueryable();

        // Exclude the current interaction if specified
        if (excludeInteractionId.HasValue)
        {
            query = query.Where(i => i.Id != excludeInteractionId.Value);
        }

        var totalInteractions = await query.CountAsync(ct);

        var channelInteractions = channelId.HasValue
            ? await query.CountAsync(i => i.ChannelId == channelId.Value, ct)
            : 0;

        // Get most used command by parsing the Data JSON field
        var allInteractions = await query
            .AsNoTracking()
            .Select(i => new { i.Data, i.UserId, i.Username, i.GlobalName })
            .ToListAsync(ct);

        var commandCounts = new Dictionary<string, int>();
        var userCounts = new Dictionary<long, (string name, int count)>();

        foreach (var interaction in allInteractions)
        {
            // Parse command name from Data JSON
            if (!string.IsNullOrWhiteSpace(interaction.Data))
            {
                try
                {
                    using var doc = JsonDocument.Parse(interaction.Data);
                    if (doc.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        var commandName = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(commandName))
                        {
                            commandCounts
                                .Entry(commandName)
                                .AndModify(count => count + 1)
                                .OrInsert(1);
                        }
                    }
                }
                catch
                {
                    // Skip malformed JSON
                }
            }

            // Count by user
            var displayName = interaction.GlobalName ?? interaction.Username;
            userCounts
                .Entry(interaction.UserId)
                .AndModify(existing => (existing.name, existing.count + 1))
                .OrInsert((displayName, 1));
        }

        var mostUsedCommand = commandCounts.Count > 0
            ? commandCounts.OrderByDescending(x => x.Value).First()
            : default;

        var topUser = userCounts.Count > 0
            ? userCounts.OrderByDescending(x => x.Value.count).First()
            : default;

        return new StatsData
        {
            TotalInteractions = totalInteractions,
            ChannelInteractions = channelInteractions,
            MostUsedCommand = mostUsedCommand.Key,
            MostUsedCommandCount = mostUsedCommand.Value,
            TopUserName = topUser.Value.name,
            TopUserId = topUser.Key,
            TopUserCount = topUser.Value.count
        };
    }
}

public class StatsData
{
    public int TotalInteractions { get; init; }
    public int ChannelInteractions { get; init; }
    public string? MostUsedCommand { get; init; }
    public int MostUsedCommandCount { get; init; }
    public string? TopUserName { get; init; }
    public long TopUserId { get; init; }
    public int TopUserCount { get; init; }
}
