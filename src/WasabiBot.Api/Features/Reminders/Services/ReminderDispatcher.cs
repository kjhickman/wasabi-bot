using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class ReminderDispatcher : BackgroundService
{
    private const int BatchSize = 10;

    private readonly ILogger<ReminderDispatcher> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReminderWakeSignal _wakeSignal;
    private readonly TimeProvider _timeProvider;

    public ReminderDispatcher(
        ILogger<ReminderDispatcher> logger,
        IServiceScopeFactory scopeFactory,
        IReminderWakeSignal wakeSignal,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _wakeSignal = wakeSignal;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tracer = scope.ServiceProvider.GetRequiredService<Tracer>();
                using var span = tracer.StartActiveSpan("reminder.dispatcher.loop");
                var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();

                var claimed = await reminderService.ClaimDueBatchAsync(BatchSize, _timeProvider.GetUtcNow(), stoppingToken);
                if (claimed.Count > 0)
                {
                    _logger.LogInformation("Dispatching {Count} due reminder(s)", claimed.Count);
                    await reminderService.SendRemindersAsync(claimed, stoppingToken);
                    continue;
                }

                var nextDue = await reminderService.GetNextDueTimeAsync(stoppingToken);
                if (nextDue == null)
                {
                    _logger.LogDebug("No pending reminders; waiting for database notification.");
                    await _wakeSignal.WaitAsync(stoppingToken);
                    continue;
                }

                var delay = nextDue.Value - _timeProvider.GetUtcNow();
                if (delay <= TimeSpan.Zero)
                {
                    continue;
                }

                _logger.LogDebug("Waiting {Delay} until next reminder at {NextDue}", delay, nextDue.Value.ToString("O"));
                var wokeEarly = await _wakeSignal.WaitAsync(delay, stoppingToken);
                if (wokeEarly)
                {
                    _logger.LogDebug("Reminder schedule changed; recalculating next due reminder.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in ReminderDispatcher loop");
            }
        }

        _logger.LogInformation("ReminderDispatcher stopping");
    }
}
