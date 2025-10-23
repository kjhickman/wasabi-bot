using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;
using WasabiBot.ServiceDefaults;

namespace WasabiBot.Api.Features.RemindMe;

internal sealed class RemindMeCommand : CommandBase
{
    private readonly IChatClient _chatClient;
    private readonly Tracer _tracer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReminderTimeCalculator _reminderTimeCalculator;
    private readonly ILogger<RemindMeCommand> _logger;

    public RemindMeCommand(
        IChatClient chatClient,
        Tracer tracer,
        IServiceScopeFactory scopeFactory,
        IReminderTimeCalculator reminderTimeCalculator,
        ILogger<RemindMeCommand> logger)
    {
        _chatClient = chatClient;
        _tracer = tracer;
        _scopeFactory = scopeFactory;
        _reminderTimeCalculator = reminderTimeCalculator;
        _logger = logger;
    }

    public override string Command => "remindme";
    public override string Description => "Set a reminder for yourself.";

    [CommandEntry]
    public Task HandleAsync(
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "when", Description = "When should I remind you? e.g., 'in 2 hours' or '10/31 5pm'")] string when,
        [SlashCommandParameter(Name = "reminder", Description = "What should I remind you about?")] string reminder)
    {
        var commandContext = new DiscordCommandContext(ctx);
        return ExecuteAsync(commandContext, when, reminder);
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        string when,
        string reminder)
    {
        using var span = _tracer.StartActiveSpan("remindme.schedule.request");

        var user = ctx.Interaction.User;
        var userDisplayName = user.GlobalName ?? user.Username;
        var userId = user.Id;
        var channelId = ctx.Interaction.Channel.Id;

        _logger.LogInformation(
            "Reminder command invoked by user {User} in channel {ChannelId} with when='{When}' and reminder='{Reminder}'",
            userDisplayName,
            channelId,
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
            var relative = AIFunctionFactory.Create(_reminderTimeCalculator.ComputeRelativeUtc);
            var absolute = AIFunctionFactory.Create(_reminderTimeCalculator.ComputeAbsoluteUtc);
            var chatOptions = new ChatOptions { Tools = [relative, absolute] };

            var llmStart = Stopwatch.GetTimestamp();
            var response = await _chatClient.GetResponseAsync(messages, chatOptions);
            var elapsed = Stopwatch.GetElapsedTime(llmStart).TotalSeconds;
            LlmMetrics.LlmResponseLatency.Record(elapsed, new TagList
            {
                {"command", Command},
                {"status", "ok"}
            });
            var toolMessage = response.Messages.FirstOrDefault(x => x.Role == ChatRole.Tool);
            if (toolMessage?.Contents.FirstOrDefault() is not FunctionResultContent functionResult)
            {
                _logger.LogWarning(
                    "AI tool failed to return a result for reminder request from user {User}",
                    userDisplayName);
                await ctx.SendEphemeralAsync("Failed to get a response from the AI tool.");
                return;
            }

            var raw = functionResult.Result?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning(
                    "AI tool returned an empty result for reminder request from user {User}",
                    userDisplayName);
                await ctx.SendEphemeralAsync("I couldn't interpret that timeframe.");
                return;
            }

            if (DateTimeOffset.TryParse(raw, out var targetTime))
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var currentMinute = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, 0, TimeSpan.Zero);

                if (targetTime <= currentMinute)
                {
                    _logger.LogWarning(
                        "Reminder time {TargetTime} rejected because it is not far enough in the future (user {User})",
                        targetTime.ToString("O"),
                        userDisplayName);
                    await ctx.SendEphemeralAsync("Only future times are allowed.");
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    var scheduled = await reminderService.ScheduleAsync(userId, channelId, reminder, targetTime);

                    if (!scheduled)
                    {
                        _logger.LogError(
                            "Failed to persist reminder for user {User} targeting {TargetTime}",
                            userDisplayName,
                            targetTime.ToString("O"));
                        await ctx.SendEphemeralAsync("Reminder failed to save. Please try again later.");
                        return;
                    }
                }

                var unixTimeSeconds = targetTime.ToUnixTimeSeconds();
                _logger.LogInformation(
                    "Reminder scheduled for user {User} at {TargetTime}",
                    userDisplayName,
                    targetTime.ToString("O"));
                await ctx.RespondAsync($"I'll remind you <t:{unixTimeSeconds}:f> to *{reminder}*");
            }
            else
            {
                _logger.LogWarning(
                    "Failed to parse AI-produced timestamp '{Raw}' for user {User}",
                    raw,
                    userDisplayName);
                await ctx.SendEphemeralAsync("Something went wrong interpreting that timeframe.");
            }
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(
                ex,
                "Unhandled error while processing reminder for user {User}",
                userDisplayName);
            await ctx.SendEphemeralAsync("Failed to process reminder.");
        }
    }
}
