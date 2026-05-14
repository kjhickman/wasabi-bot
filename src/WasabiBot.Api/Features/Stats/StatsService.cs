using System.Text.Json;
using Dapper;
using DictionaryEntry;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Stats;

public interface IStatsService
{
    Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default);
}

public class StatsService : IStatsService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly Tracer _tracer;

    public StatsService(NpgsqlDataSource dataSource, Tracer tracer)
    {
        _dataSource = dataSource;
        _tracer = tracer;
    }

    public async Task<StatsData> GetStatsAsync(long? channelId = null, long? excludeInteractionId = null, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan("stats.compute");
        if (channelId.HasValue)
            span.SetAttribute("stats.channel_id", channelId.Value.ToString());
        if (excludeInteractionId.HasValue)
            span.SetAttribute("stats.exclude_interaction_id", excludeInteractionId.Value);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        var totalInteractions = excludeInteractionId.HasValue
            ? await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM \"Interactions\" WHERE \"Id\" <> @ExcludeInteractionId",
                new { ExcludeInteractionId = excludeInteractionId.Value }, cancellationToken: ct))
            : await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM \"Interactions\"", cancellationToken: ct));

        var channelInteractions = (channelId, excludeInteractionId) switch
        {
            (long channel, long excluded) => await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM \"Interactions\" WHERE \"ChannelId\" = @ChannelId AND \"Id\" <> @ExcludeInteractionId",
                new { ChannelId = channel, ExcludeInteractionId = excluded }, cancellationToken: ct)),
            (long channel, null) => await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM \"Interactions\" WHERE \"ChannelId\" = @ChannelId",
                new { ChannelId = channel }, cancellationToken: ct)),
            _ => 0
        };

        var allInteractions = excludeInteractionId.HasValue
            ? await connection.QueryAsync<InteractionRow>(new CommandDefinition(
                "SELECT \"Id\", \"ChannelId\", \"ApplicationId\", \"UserId\", \"GuildId\", \"Username\", \"GlobalName\", \"Nickname\", \"Data\", \"CreatedAt\" FROM \"Interactions\" WHERE \"Id\" <> @ExcludeInteractionId",
                new { ExcludeInteractionId = excludeInteractionId.Value }, cancellationToken: ct))
            : await connection.QueryAsync<InteractionRow>(new CommandDefinition(
                "SELECT \"Id\", \"ChannelId\", \"ApplicationId\", \"UserId\", \"GuildId\", \"Username\", \"GlobalName\", \"Nickname\", \"Data\", \"CreatedAt\" FROM \"Interactions\"",
                cancellationToken: ct));

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
                .OrInsert((displayName!, 1));
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
