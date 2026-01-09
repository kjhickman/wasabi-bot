using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Reminders;

[CommandHandler("reminder", "Set a reminder for the channel.")]
internal sealed class RemindMeCommand
{
    private readonly ILogger<RemindMeCommand> _logger;
    private readonly IReminderService _reminderService;
    private readonly ITimeParsingService _timeParsingService;
    private readonly TimeProvider _timeProvider;

    public RemindMeCommand(
        ILogger<RemindMeCommand> logger,
        IReminderService reminderService,
        ITimeParsingService timeParsingService,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _reminderService = reminderService;
        _timeParsingService = timeParsingService;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        [SlashCommandParameter(Description = "When do you want to be reminded?")] string when,
        [SlashCommandParameter(Description = "Your reminder")] string reminder)
    {
        var whenText = when.Trim();
        var reminderText = reminder.Trim();

        _logger.LogInformation(
            "Reminder command invoked by {User} ({UserId}) in channel {ChannelId} with when='{When}'",
            ctx.UserDisplayName,
            ctx.UserId,
            ctx.ChannelId,
            whenText);

        DateTimeOffset remindAt;
        try
        {
            remindAt = await _timeParsingService.ParseTimeAsync(whenText) ?? throw new InvalidOperationException("Time parser returned no value.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse reminder time for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Sorry, I couldn't understand that time. Try phrases like 'in 30 minutes' or 'tomorrow at 9am'.");
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (remindAt <= now)
        {
            await ctx.SendEphemeralAsync("Please choose a time in the future.");
            return;
        }

        var scheduled = await _reminderService.ScheduleAsync(ctx.UserId, ctx.ChannelId, reminderText, remindAt);
        if (!scheduled)
        {
            _logger.LogWarning("Failed to schedule reminder for user {UserId} in channel {ChannelId}", ctx.UserId, ctx.ChannelId);
            await ctx.SendEphemeralAsync("I couldn't schedule that reminder. Please try again.");
            return;
        }

        var unixTimestamp = remindAt.ToUnixTimeSeconds();
        await ctx.RespondAsync($"I'll remind you <t:{unixTimestamp}:f>: {reminderText}");
    }
}

