using System.ComponentModel;
using Microsoft.Extensions.AI;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Hosting.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.Api.Services;

namespace WasabiBot.Api.Modules;

internal sealed class RemindMeCommand : ISlashCommand
{
    public string Name => "remindme";
    public string Description => "Set a reminder for yourself.";

    private static readonly ChatOptions ChatOptions;

    static RemindMeCommand()
    {
        var relativeTimeFunction = AIFunctionFactory.Create(GetTargetUtcTime);
        var absoluteTimeFunction = AIFunctionFactory.Create(GetAbsoluteUtcTime);
        ChatOptions = new ChatOptions { Tools = [relativeTimeFunction, absoluteTimeFunction] };
    }

    public void Register(WebApplication app)
    {
        app.AddSlashCommand(Name, Description, ExecuteAsync);
    }

    [Description(
        "Computes the target UTC DateTime by adding the provided relative offsets to the current time (UTC now).")]
    private static DateTimeOffset GetTargetUtcTime(
        [Description("Number of months from now (0 if not specified).")]
        int months = 0,
        [Description("Number of weeks from now (0 if not specified).")]
        int weeks = 0,
        [Description("Number of days from now (0 if not specified).")]
        int days = 0,
        [Description("Number of hours from now (0 if not specified).")]
        int hours = 0,
        [Description("Number of minutes from now (0 if not specified).")]
        int minutes = 0,
        [Description("Number of seconds from now (0 if not specified).")]
        int seconds = 0
    )
    {
        if (months < 0 || weeks < 0 || days < 0 || hours < 0 || minutes < 0 || seconds < 0 ||
            (months == 0 && weeks == 0 && days == 0 && hours == 0 && minutes == 0 && seconds == 0))
        {
            return DateTimeOffset.MinValue;
        }

        return DateTimeOffset.UtcNow
            .AddMonths(months)
            .AddDays(weeks * 7 + days)
            .AddHours(hours)
            .AddMinutes(minutes)
            .AddSeconds(seconds);
    }

    [Description("Computes the target UTC DateTime from explicit calendar components, inferring upcoming dates when needed.")]
    private static DateTimeOffset GetAbsoluteUtcTime(
        [Description("Calendar month 1-12.")]
        int month,
        [Description("Calendar day 1-31.")]
        int day,
        [Description("Calendar year (0 if unspecified).")]
        int year = 0,
        [Description("Hour 0-23 (-1 if unspecified).")]
        int hour = -1,
        [Description("Minute 0-59  (-1 if unspecified).")]
        int minute = -1,
        [Description("IANA or Windows time zone identifier, defaults to US Central when omitted.")]
        string? timeZoneId = null)
    {
        if (month is < 1 or > 12 || day < 1 || hour < -1 || hour > 23 || minute < -1 || minute > 59)
        {
            return DateTimeOffset.MinValue;
        }

        var timeZone = GetTimeZone(timeZoneId);
        var now = DateTimeOffset.UtcNow.ToOffset(timeZone.BaseUtcOffset);
        if (year == 0)
        {
            year = now.Year;
        }

        if (year < now.Year)
        {
            return DateTimeOffset.MinValue;
        }

        if (hour == -1 && minute == -1)
        {
            hour = now.Hour;
            minute = now.Minute;
        }

        if (hour == -1)
        {
            hour = 0;
        }

        if (minute == -1)
        {
            minute = 0;
        }

        var localDateTime = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(localDateTime);
        var dateTimeOffset = new DateTimeOffset(localDateTime, offset);
        if (dateTimeOffset < now)
        {
            dateTimeOffset = dateTimeOffset.AddYears(1);
        }

        return dateTimeOffset.ToUniversalTime();
    }

    private static TimeZoneInfo GetTimeZone(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
    }

    private async Task ExecuteAsync(IChatClient chat, Tracer tracer, IReminderService reminderService, ApplicationCommandContext ctx, string when, string reminder)
    {
        using var span = tracer.StartActiveSpan($"{nameof(RemindMeCommand)}.{nameof(ExecuteAsync)}");

        await using var responder = new AutoResponder(
            threshold: TimeSpan.FromMilliseconds(2300),
            defer: _ => ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage()),
            respond: (text, ephemeral) => ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionMessageFactory.Create(text, ephemeral))),
            followup: (text, ephemeral) => ctx.Interaction.SendFollowupMessageAsync(InteractionMessageFactory.Create(text, ephemeral)));

        const string systemInstructions = "The user gives a natural language timeframe. Select the best tool: " +
                                          "Use GetTargetUtcTime for relative offsets like 'in 3 hours', " +
                                          "or GetAbsoluteUtcTime when the user specifies a calendar date or explicit time. " +
                                          "If AM/PM aren't specified, try to think which is more reasonable." +
                                          "After the tool returns a timestamp, simply end the response. " +
                                          "You don't need to respond further.";

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
                await responder.SendAsync("Failed to get a response from the AI tool.", ephemeral: true);
                return;
            }
            if (toolMessage.Contents.FirstOrDefault() is not FunctionResultContent functionResult)
            {
                await responder.SendAsync("The AI did not call the expected function.", ephemeral: true);
                return;
            }

            if (DateTimeOffset.TryParse(functionResult.Result!.ToString()!, out var targetTime))
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
