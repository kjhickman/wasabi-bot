using System.ComponentModel;
using Microsoft.Extensions.AI;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Modules;

internal static class RemindMe
{
    public const string CommandName = "remindme";
    public const string CommandDescription = "Prototype: parse timeframe using AI tool call and echo result.";

    private static readonly ChatOptions? ChatOptions;

    static RemindMe()
    {
        var computeTimeFunction = AIFunctionFactory.Create(GetTargetUtcTime);
        ChatOptions = new ChatOptions { Tools = [computeTimeFunction] };
    }

    [Description("Computes the target UTC DateTime by adding the provided relative offsets to the current time (UTC now)." )]
    private static DateTimeOffset GetTargetUtcTime(
        [Description("Number of months from now (0 if not specified)." )] int months = 0,
        [Description("Number of weeks from now (0 if not specified)." )] int weeks = 0,
        [Description("Number of days from now (0 if not specified)." )] int days = 0,
        [Description("Number of hours from now (0 if not specified)." )] int hours = 0,
        [Description("Number of minutes from now (0 if not specified)." )] int minutes = 0,
        [Description("Number of seconds from now (0 if not specified)." )] int seconds = 0
    )
    {
        return DateTimeOffset.UtcNow
            .AddMonths(months)
            .AddDays(weeks * 7 + days)
            .AddHours(hours)
            .AddMinutes(minutes)
            .AddSeconds(seconds);
    }

    // Reordered parameters: timeframe first, then reminder text for intuitive usage.
    public static async Task Command(IChatClient chat, Tracer tracer, ApplicationCommandContext ctx, string when, string reminder)
    {
        using var span = tracer.StartActiveSpan($"{nameof(RemindMe)}.{nameof(Command)}");
        await ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var systemInstructions =
            "The user gives a natural language timeframe. You MUST call GetTargetUtcTime with a reasonable breakdown of months/weeks/days/hours/minutes/seconds. " +
            "After the tool returns a timestamp, respond ONLY in one concise line: 'Target UTC: <ISO8601> (in <friendly relative>)'. " +
            "The friendly relative should be a compact human readable form (e.g., '2h 30m', '3d', '1w 2d 4h'). Do not add extra commentary.";

        var userMessage = $"Timeframe: {when}";

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemInstructions),
                new(ChatRole.User, userMessage)
            };

            var response = await chat.GetResponseAsync(messages, ChatOptions);
            var toolMessage = response.Messages.FirstOrDefault(x => x.Role == ChatRole.Tool);
            if (toolMessage is null)
            {
                await ctx.Interaction.SendFollowupMessageAsync("Failed to get a response from the AI tool.");
                return;
            }
            if (toolMessage.Contents.FirstOrDefault() is not FunctionResultContent functionResult)
            {
                await ctx.Interaction.SendFollowupMessageAsync("The AI did not call the expected function.");
                return;
            }

            if (DateTimeOffset.TryParse(functionResult.Result!.ToString()!, out var targetTime))
            {
                var unixTimeSeconds = targetTime.ToUnixTimeSeconds();
                await ctx.Interaction.SendFollowupMessageAsync($"I'll remind you <t:{unixTimeSeconds}:R> to *{reminder}*");
            }
            else
            {
                await ctx.Interaction.SendFollowupMessageAsync("Could not parse tool result into a date/time.");
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            await ctx.Interaction.SendFollowupMessageAsync($"Failed to process timeframe. Error: {ex.Message}\nOriginal timeframe: {when}\nReminder: {reminder}");
        }
    }
}
