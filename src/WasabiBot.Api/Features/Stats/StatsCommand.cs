using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Stats;

[CommandHandler("stats", "Show bot usage statistics.")]
internal sealed class StatsCommand
{
    private readonly ILogger<StatsCommand> _logger;
    private readonly IStatsService _statsService;

    public StatsCommand(ILogger<StatsCommand> logger, IStatsService statsService)
    {
        _logger = logger;
        _statsService = statsService;
    }

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            _logger.LogInformation("Stats command invoked by user {User} ({UserId}) in channel {ChannelId}",
                ctx.UserDisplayName, ctx.UserId, ctx.ChannelId);

            var stats = await _statsService.GetStatsAsync((long)ctx.ChannelId, (long)ctx.InteractionId);

            var message = BuildStatsMessage(stats);
            await ctx.RespondAsync(message, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stats command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while processing that command. Please try again later.");
        }
    }

    private static string BuildStatsMessage(StatsData stats)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# 📊 Bot Statistics");
        sb.AppendLine();

        sb.AppendLine("## Interaction Counts");
        sb.AppendLine($"• **Total interactions:** {stats.TotalInteractions:N0}");
        sb.AppendLine($"• **This channel:** {stats.ChannelInteractions:N0}");

        if (!string.IsNullOrWhiteSpace(stats.MostUsedCommand))
        {
            sb.AppendLine("## Most Used Command");
            sb.AppendLine($"• **`/{stats.MostUsedCommand}`** - {stats.MostUsedCommandCount:N0} uses");
        }

        if (!string.IsNullOrWhiteSpace(stats.TopUserName))
        {
            sb.AppendLine("## Top User");
            sb.AppendLine($"• **{stats.TopUserName}** - {stats.TopUserCount:N0} commands");
        }

        return sb.ToString();
    }
}

