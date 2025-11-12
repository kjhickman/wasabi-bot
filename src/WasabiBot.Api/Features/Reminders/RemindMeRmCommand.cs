using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Reminders;

[CommandHandler("remindme-rm", "Delete a reminder.", nameof(ExecuteAsync))]
internal sealed class RemindMeRmCommand
{
    private readonly ILogger<RemindMeRmCommand> _logger;

    public RemindMeRmCommand(ILogger<RemindMeRmCommand> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        int reminderId)
    {
        var userDisplayName = ctx.UserDisplayName;
        var userId = ctx.UserId;

        _logger.LogInformation(
            "Reminder delete command invoked by user {User} ({UserId}) for reminder {ReminderId}",
            userDisplayName,
            userId,
            reminderId);

        await ctx.RespondAsync("This command is not yet implemented.");
    }
}

