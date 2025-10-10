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
        string when,
        string reminder)
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
            if (toolMessage is null)
            {
                await responder.SendAsync("Failed to get a response from the AI tool.", ephemeral: true);
                return;
            }
            if (toolMessage.Contents.FirstOrDefault() is not FunctionResultContent functionResult)
            {
                await responder.SendAsync("The AI did not call the expected function.", ephemeral: true);
                return;
            }

            var raw = functionResult.Result?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                await responder.SendAsync("I couldn't interpret that timeframe.", ephemeral: true);
                return;
            }

            if (DateTimeOffset.TryParse(raw, out var targetTime))
            {
                if (targetTime <= DateTimeOffset.UtcNow.AddSeconds(30))
                {
                    await responder.SendAsync("Only future times are allowed.", ephemeral: true);
                    return;
                }

                var created = await reminderService.CreateAsync(
                    userId: (long)ctx.Interaction.User.Id,
                    channelId: (long)ctx.Interaction.Channel.Id,
                    reminder: reminder,
                    remindAt: targetTime);

                if (!created)
                {
                    await responder.SendAsync("Failed to store reminder in database.", ephemeral: true);
                    return;
                }

                var unixTimeSeconds = targetTime.ToUnixTimeSeconds();
                await responder.SendAsync($"I'll remind you <t:{unixTimeSeconds}:f> to *{reminder}*");
            }
            else
            {
                await responder.SendAsync("Could not parse tool result into a date/time.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            await responder.SendAsync($"Failed to process timeframe. Error: {ex.Message}\nOriginal timeframe: {when}\nReminder: {reminder}", ephemeral: true);
        }
    }
}
