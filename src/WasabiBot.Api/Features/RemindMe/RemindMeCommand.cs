using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.Api.Infrastructure.Discord.Interactions;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.Features.RemindMe;

internal static class RemindMeCommand
{
    public const string Name = "remindme";
    public const string Description = "Set a reminder for yourself.";

    public static async Task ExecuteAsync(
        IChatClient chat,
        Tracer tracer,
        IReminderService reminderService,
        ReminderTimeCalculator reminderTimeCalculator,
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "when", Description = "When should I remind you? e.g., 'in 2 hours' or '10/31 5pm'")] string when,
        [SlashCommandParameter(Name = "reminder", Description = "What should I remind you about?")] string reminder)
    {
        using var span = tracer.StartActiveSpan($"{nameof(RemindMeCommand)}.{nameof(ExecuteAsync)}");
        await using var responder = InteractionResponder.Create(ctx);

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
                await responder.SendEphemeralAsync("Failed to get a response from the AI tool.");
                return;
            }

            var raw = functionResult.Result?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                await responder.SendEphemeralAsync("I couldn't interpret that timeframe.");
                return;
            }

            if (DateTimeOffset.TryParse(raw, out var targetTime))
            {
                if (targetTime <= DateTimeOffset.UtcNow.AddSeconds(30))
                {
                    await responder.SendEphemeralAsync("Only future times are allowed.");
                    return;
                }

                var created = await reminderService.CreateAsync(
                    userId: (long)ctx.Interaction.User.Id,
                    channelId: (long)ctx.Interaction.Channel.Id,
                    reminder: reminder,
                    remindAt: targetTime);

                if (!created)
                {
                    await responder.SendEphemeralAsync("Reminder failed to save. Please try again later.");
                    return;
                }

                var unixTimeSeconds = targetTime.ToUnixTimeSeconds();
                await responder.SendAsync($"I'll remind you <t:{unixTimeSeconds}:f> to *{reminder}*");
            }
            else
            {
                await responder.SendEphemeralAsync("Something went wrong interpreting that timeframe.");
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            await responder.SendEphemeralAsync($"Failed to process reminder.");
        }
    }
}
