using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Features.RemindMe.Contracts;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderService : IReminderService
{
    private readonly ITimeTickerManager<TimeTicker> _tickerManager;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(ITimeTickerManager<TimeTicker> tickerManager, ILogger<ReminderService> logger)
    {
        _tickerManager = tickerManager;
        _logger = logger;
    }

    public async Task<bool> ScheduleAsync(CreateReminderRequest request)
    {
        _logger.LogInformation("Scheduling reminder for {UserId} at {RemindAt}", request.UserId, request.RemindAt);

        var result = await _tickerManager.AddAsync(new TimeTicker
        {
            Function = SendReminderFunction.FunctionName,
            Request = TickerHelper.CreateTickerRequest(request),
            ExecutionTime = request.RemindAt.UtcDateTime
        });

        return result.IsSucceded;
    }
}
