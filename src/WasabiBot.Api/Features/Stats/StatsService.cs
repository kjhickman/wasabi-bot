using System.Text.Json;
using DictionaryEntry;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using WasabiBot.Api.Persistence;

namespace WasabiBot.Api.Features.Stats;

public interface IStatsService
{
    Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default);
}

public class StatsService : IStatsService
{
    private readonly WasabiBotContext _context;
    private readonly Tracer _tracer;

    public StatsService(WasabiBotContext context, Tracer tracer)
    {
        _context = context;
        _tracer = tracer;
    }

    public async Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("stats.compute");
        if (channelId.HasValue)
            span.SetAttribute("stats.channel_id", channelId.Value.ToString());
        if (excludeInteractionId.HasValue)
            span.SetAttribute("stats.exclude_interaction_id", excludeInteractionId.Value);

        var totalInteractions = excludeInteractionId.HasValue
            ? await _context.Interactions.AsNoTracking().CountAsync(i => i.Id != excludeInteractionId.Value, ct)
            : await _context.Interactions.AsNoTracking().CountAsync(ct);

        var channelInteractions = (channelId, excludeInteractionId) switch
        {
            (long channel, long excluded) => await _context.Interactions.AsNoTracking().CountAsync(i => i.ChannelId == channel && i.Id != excluded, ct),
            (long channel, null) => await _context.Interactions.AsNoTracking().CountAsync(i => i.ChannelId == channel, ct),
            _ => 0
        };

        // Get most used command by parsing the Data JSON field
        var allInteractions = excludeInteractionId.HasValue
            ? await _context.Interactions.AsNoTracking().Where(i => i.Id != excludeInteractionId.Value).ToListAsync(ct)
            : await _context.Interactions.AsNoTracking().ToListAsync(ct);

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
