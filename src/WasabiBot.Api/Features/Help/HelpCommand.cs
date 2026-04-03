using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Help;

[CommandHandler("help", "Shows all available commands and helpful links.")]
internal sealed class HelpCommand(ILogger<HelpCommand> logger)
{
    private readonly ILogger<HelpCommand> _logger = logger;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            _logger.LogInformation("Help command invoked by user {User} ({UserId})",
                ctx.UserDisplayName, ctx.UserId);

            var helpMessage = BuildHelpMessage();
            await ctx.RespondAsync(helpMessage, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Help command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while processing that command. Please try again later.");
        }
    }

    private static string BuildHelpMessage()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Wasabi Bot Help");
        sb.AppendLine();
        sb.AppendLine("## Available Commands");
        sb.AppendLine();

        // Music commands
        sb.AppendLine("### Music");
        sb.AppendLine("• `/play` - Play music from a SoundCloud search query or track/playlist URL");
        sb.AppendLine("• `/skip` - Skip the current track");
        sb.AppendLine("• `/stop` - Stop playback and clear the queue");
        sb.AppendLine("• `/queue` - Show the current queue");
        sb.AppendLine("• `/nowplaying` - Show the currently playing track");
        sb.AppendLine("• `/leave` - Disconnect from the voice channel");

        // Reminder commands
        sb.AppendLine("### Reminders");
        sb.AppendLine("• `/reminder` - Set a reminder for the channel");
        sb.AppendLine("• `/reminder-list` - List all your reminders");
        sb.AppendLine("• `/reminder-rm` - Delete a reminder by ID");

        // Fun commands
        sb.AppendLine("### Fun");
        sb.AppendLine("• `/conch` - Ask the magic conch a yes/no question");
        sb.AppendLine("• `/choose` - Choose randomly from 2-7 options");
        sb.AppendLine("• `/flip` - Flip a coin");
        sb.AppendLine("• `/caption` - Generate a funny caption for an image");

        // Utility commands
        sb.AppendLine("### Utility");
        sb.AppendLine("• `/ask` - Ask any question");
        sb.AppendLine("• `/stats` - Show bot usage statistics");
        sb.AppendLine("• `/help` - Shows this help message");

        // Links section
        sb.AppendLine("## Helpful Links");
        sb.AppendLine("• [Website for API access](<https://wasabibot.com>)");
        sb.AppendLine("• [GitHub Repository](<https://github.com/kjhickman/wasabi-bot>)");

        return sb.ToString();
    }
}
