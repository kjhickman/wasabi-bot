using OpenTelemetry.Trace;
using WasabiBot.Api.Features.RemindMe.Abstractions;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderProcessor : BackgroundService
{
    private readonly ILogger<ReminderProcessor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PendingReminderStore _store;
    private readonly TimeProvider _timeProvider;

    public ReminderProcessor(ILogger<ReminderProcessor> logger, IServiceScopeFactory scopeFactory,
        PendingReminderStore store, TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _store = store;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await LoadAllAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextDue = _store.NextDueTime;
                if (nextDue == null)
                {
                    _logger.LogDebug("No reminders queued; awaiting first reminder.");
                    await _store.WaitForEarlierAsync(stoppingToken);
                    continue; // re-evaluate next due time
                }

                var now = _timeProvider.GetUtcNow();
                if (nextDue <= now)
                {
                    await ProcessDueAsync(stoppingToken);
                    continue; // re-evaluate next due time
                }

                var delay = nextDue.Value - now;
                _logger.LogDebug("Waiting {Delay} until next due reminder at {NextDue}", delay, nextDue.Value.ToString("O"));

                // Wait either for delay to elapse OR an earlier reminder to be inserted.
                var delayTask = Task.Delay(delay, stoppingToken);
                var signalTask = _store.WaitForEarlierAsync(stoppingToken);
                var completed = await Task.WhenAny(delayTask, signalTask);
                if (completed == signalTask)
                {
                    _logger.LogDebug("Earlier reminder scheduled; reevaluating next due time.");
                    continue; // re-evaluate next due time
                }

                // Delay elapsed; process due reminders.
                await ProcessDueAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ReminderProcessor loop");
        }

        _logger.LogInformation("ReminderProcessor stopping");
    }

    private async Task LoadAllAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        using var span = scope.ServiceProvider.GetRequiredService<Tracer>()
            .StartActiveSpan($"{nameof(ReminderProcessor)}.{nameof(LoadAllAsync)}");
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var reminders = await reminderService.GetAllUnsent(ct);
        _store.InsertMany(reminders);
        _logger.LogInformation("Loaded {Count} reminder(s). Next due: {NextDue}", reminders.Count, _store.NextDueTime?.ToString("O") ?? "<none>");
    }

    private async Task ProcessDueAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        using var span = scope.ServiceProvider.GetRequiredService<Tracer>()
            .StartActiveSpan($"{nameof(ReminderProcessor)}.{nameof(ProcessDueAsync)}");

        var now = _timeProvider.GetUtcNow();
        var due = _store.GetAllDueReminders(now);
        if (due.Count == 0)
        {
            _logger.LogInformation("No due reminders at {Now}", now.ToString("O"));
            return;
        }

        _logger.LogInformation("Processing {Count} due reminder(s) at {Now}", due.Count, now.ToString("O"));
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var sentIds = await reminderService.SendRemindersAsync(due, ct);
        foreach (var id in sentIds)
        {
            _store.RemoveById(id);
        }
    }
}
