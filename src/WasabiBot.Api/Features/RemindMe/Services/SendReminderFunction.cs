using NetCord.Rest;
using OpenTelemetry.Trace;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Models;
using WasabiBot.Api.Features.RemindMe.Contracts;

namespace WasabiBot.Api.Features.RemindMe.Services;

public class SendReminderFunction
{
    public const string FunctionName = "SendReminder";

    private readonly ILogger<SendReminderFunction> _logger;
    private readonly RestClient _discordClient;
    private readonly Tracer _tracer;

    public SendReminderFunction(ILogger<SendReminderFunction> logger, RestClient discordClient, Tracer tracer)
    {
        _logger = logger;
        _discordClient = discordClient;
        _tracer = tracer;
    }

    [TickerFunction(functionName: FunctionName)]
    public async Task ExecuteAsync(TickerFunctionContext<CreateReminderRequest> context)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(SendReminderFunction)}.{nameof(ExecuteAsync)}");
        _logger.LogInformation("Sending reminder to user {UserId} in channel {ChannelId}", context.Request.UserId,
            context.Request.ChannelId);

        var reminder = context.Request;
        await _discordClient.SendMessageAsync((ulong)reminder.ChannelId,
            $"<@{reminder.UserId}> Reminder: {reminder.ReminderMessage}");
    }
}
