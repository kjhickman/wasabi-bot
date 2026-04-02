using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Reminders;

[CommandHandler("reminder-rm", "Delete a reminder.")]
internal sealed class RemindMeRmCommand
{
    private readonly ILogger<RemindMeRmCommand> _logger;
    private readonly IReminderService _reminderService;
    private readonly Tracer _tracer;

    public RemindMeRmCommand(ILogger<RemindMeRmCommand> logger, IReminderService reminderService, Tracer tracer)
    {
        _logger = logger;
        _reminderService = reminderService;
        _tracer = tracer;
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        int reminderId)
    {
        using var span = _tracer.StartActiveSpan("reminder.command.delete");
        span.SetAttribute("discord.user_id", ctx.UserId.ToString());
        span.SetAttribute("reminder.id", reminderId);

        try
        {
            var userDisplayName = ctx.UserDisplayName;
            var userId = ctx.UserId;

            _logger.LogInformation(
                "Reminder delete command invoked by user {User} ({UserId}) for reminder {ReminderId}",
                userDisplayName,
                userId,
                reminderId);

            // First, check if the reminder exists and belongs to the user
            var reminder = await _reminderService.GetByIdAsync(reminderId);

            if (reminder == null)
            {
                await ctx.RespondAsync($"❌ Reminder `{reminderId}` does not exist.", ephemeral: true);
                return;
            }

            if (reminder.UserId != (long)userId)
            {
                await ctx.RespondAsync($"❌ You may only delete your own reminders.", ephemeral: true);
                _logger.LogWarning("User {UserId} attempted to delete reminder {ReminderId} owned by user {OwnerId}",
                    userId, reminderId, reminder.UserId);
                return;
            }

            var deleted = await _reminderService.DeleteByIdAsync(reminderId);

            if (deleted)
            {
                await ctx.RespondAsync($"✅ Reminder `{reminderId}` has been deleted.", ephemeral: true);
                _logger.LogInformation("User {UserId} deleted reminder {ReminderId}", userId, reminderId);
            }
            else
            {
                await ctx.RespondAsync($"❌ Failed to delete reminder `{reminderId}`. Please try again.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Reminder delete command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while processing that command. Please try again later.");
        }
    }
}
