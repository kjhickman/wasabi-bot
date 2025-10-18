using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.RemindMe;

internal class RemindMeCommand
{
    public const string Name = "remindme";
    public const string Description = "Set a reminder for yourself.";

    public static async Task ExecuteAsync(
        IChatClient chat,
        Tracer tracer,
        IReminderService reminderService,
        IReminderTimeCalculator reminderTimeCalculator,
        ILogger<RemindMeCommand> logger,
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "when", Description = "When should I remind you? e.g., 'in 2 hours' or '10/31 5pm'")] string when,
        [SlashCommandParameter(Name = "reminder", Description = "What should I remind you about?")] string reminder)
    {
        using var span = tracer.StartActiveSpan("remindme.schedule.request");
        await using var responder = InteractionResponder.Create(ctx);

        var userDisplayName = ctx.Interaction.User.GlobalName ?? ctx.Interaction.User.Username;
        logger.LogInformation(
            "Reminder command invoked by user {User} in channel {ChannelId} with when='{When}' and reminder='{Reminder}'",
            userDisplayName,
            ctx.Interaction.Channel.Id,
            when,
            reminder);

        const string systemInstructions = "The user gives a natural language timeframe. Select the best tool: " +
                                          "Use RelativeTime (relative offsets) when phrased like 'in 3 hours', 'after 2 days'. " +
                                          "Use AbsoluteTime when specifying calendar components (month/day[/year] [time] [zone]). " +
                                          "If AM/PM aren't specified, pick a reasonable assumption (prefer future). " +
                                          "After calling exactly one tool, end the response with ONLY the resulting ISO timestamp. " +
                                          "Do not add explanation or narrative.";

        var userMessage = $"Timeframe: {when}";

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemInstructions),
                new(ChatRole.User, userMessage)
            };

            // TODO: figure out pattern for injecting tools via DI
            var relative = AIFunctionFactory.Create(reminderTimeCalculator.ComputeRelativeUtc);
            var absolute = AIFunctionFactory.Create(reminderTimeCalculator.ComputeAbsoluteUtc);
            var chatOptions = new ChatOptions { Tools = [relative, absolute] };

            var response = await chat.GetResponseAsync(messages, chatOptions);
            var toolMessage = response.Messages.FirstOrDefault(x => x.Role == ChatRole.Tool);
            if (toolMessage?.Contents.FirstOrDefault() is not FunctionResultContent functionResult)
            {
                logger.LogWarning(
                    "AI tool failed to return a result for reminder request from user {User}",
                    userDisplayName);
                await responder.SendEphemeralAsync("Failed to get a response from the AI tool.");
                return;
            }

            var raw = functionResult.Result?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                logger.LogWarning(
                    "AI tool returned an empty result for reminder request from user {User}",
                    userDisplayName);
                await responder.SendEphemeralAsync("I couldn't interpret that timeframe.");
                return;
            }

            if (DateTimeOffset.TryParse(raw, out var targetTime))
            {
                if (targetTime <= DateTimeOffset.UtcNow.AddSeconds(30))
                {
                    logger.LogWarning(
                        "Reminder time {TargetTime} rejected because it is not far enough in the future (user {User})",
                        targetTime.ToString("O"),
                        userDisplayName);
                    await responder.SendEphemeralAsync("Only future times are allowed.");
                    return;
                }

                var scheduled = await reminderService.ScheduleAsync(ctx.Interaction.User.Id, ctx.Interaction.Channel.Id,
                    reminder, targetTime);

                if (!scheduled)
                {
                    logger.LogError(
                        "Failed to persist reminder for user {User} targeting {TargetTime}",
                        userDisplayName,
                        targetTime.ToString("O"));
                    await responder.SendEphemeralAsync("Reminder failed to save. Please try again later.");
                    return;
                }

                var unixTimeSeconds = targetTime.ToUnixTimeSeconds();
                logger.LogInformation(
                    "Reminder scheduled for user {User} at {TargetTime}",
                    userDisplayName,
                    targetTime.ToString("O"));
                await responder.SendAsync($"I'll remind you <t:{unixTimeSeconds}:f> to *{reminder}*");
            }
            else
            {
                logger.LogWarning(
                    "Failed to parse AI-produced timestamp '{Raw}' for user {User}",
                    raw,
                    userDisplayName);
                await responder.SendEphemeralAsync("Something went wrong interpreting that timeframe.");
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            logger.LogError(
                ex,
                "Unhandled error while processing reminder for user {User}",
                userDisplayName);
            await responder.SendEphemeralAsync($"Failed to process reminder.");
        }
    }
}
