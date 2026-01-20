using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Reminders;

[CommandHandler("reminder-list", "List all your reminders.")]
internal sealed class RemindMeListCommand
{
    private readonly ILogger<RemindMeListCommand> _logger;
    private readonly IReminderService _reminderService;

    public RemindMeListCommand(ILogger<RemindMeListCommand> logger, IReminderService reminderService)
    {
        _logger = logger;
        _reminderService = reminderService;
    }

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            var userDisplayName = ctx.UserDisplayName;
            var userId = ctx.UserId;

            _logger.LogInformation(
                "Reminder list command invoked by user {User} ({UserId})",
                userDisplayName,
                userId);

            var reminders = await _reminderService.GetAllByUserId((long)userId);
            if (reminders.Count == 0)
            {
                await ctx.RespondAsync("You have no scheduled reminders.", ephemeral: true);
                return;
            }

            var response = BuildReminderListResponse(reminders);
            await ctx.RespondAsync(response, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reminder list command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while processing that command. Please try again later.");
        }
    }

    private static string BuildReminderListResponse(IReadOnlyCollection<ReminderEntity> reminders)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("**Your Scheduled Reminders:**");
        sb.AppendLine();

        foreach (var reminder in reminders)
        {
            var message = TruncateMessage(reminder.ReminderMessage, 80);
            var timestamp = FormatDiscordTimestamp(reminder.RemindAt);
            var channel = FormatChannelMention(reminder.ChannelId);

            sb.AppendLine($"**ID {reminder.Id}** - {timestamp} in {channel}");
            sb.AppendLine($"└ {message}");
            sb.AppendLine();
        }

        sb.AppendLine($"*Total: {reminders.Count} reminder(s)*");

        return sb.ToString();
    }

    private static string FormatDiscordTimestamp(DateTimeOffset remindAt)
    {
        var unixTimestamp = remindAt.ToUnixTimeSeconds();
        return $"<t:{unixTimestamp}:R>";
    }
    private static string FormatChannelMention(long channelId)
    {
        return $"<#{channelId}>";
    }


    private static string TruncateMessage(string message, int maxLength)
    {
        if (message.Length <= maxLength)
            return message;

        return message.Substring(0, maxLength - 3) + "...";
    }
}

