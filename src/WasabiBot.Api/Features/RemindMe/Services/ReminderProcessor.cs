using WasabiBot.Api.Features.RemindMe.Abstractions;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderProcessor : BackgroundService
{
    private readonly ILogger<ReminderProcessor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryReminderWindow _window;

    public ReminderProcessor(ILogger<ReminderProcessor> logger, IServiceScopeFactory scopeFactory, InMemoryReminderWindow window)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _window = window;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderProcessor starting (batch size: {BatchSize})", _window.Capacity);

        try
        {
            await LoadBatchAsync(stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing due reminders");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ReminderProcessor loop");
        }

        _logger.LogInformation("ReminderProcessor stopping");
    }

    private async Task LoadBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var reminders = await reminderService.GetDueAsync(_window.Capacity, ct);
        _window.LoadInitial(reminders);
        _logger.LogInformation("Loaded {Count} reminder(s) into memory. Last due time: {LastDue}", _window.Count, _window.LastDueTime?.ToString("O") ?? "<none>");
    }

    private async Task ProcessDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var due = _window.GetDue(now);
        if (due.Count > 0)
        {
            _logger.LogInformation("Marking {Count} reminder(s) due at or before {Now}", due.Count, now.ToString("O"));
            await using var scope = _scopeFactory.CreateAsyncScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
            try
            {
                foreach (var reminder in due)
                {
                    await reminderService.SendReminderAsync(reminder);
                    _window.RemoveById(reminder.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} reminder(s) as sent", due.Count);
                // Do not remove from window; retry next minute
            }
        }

        if (_window.NeedsRefresh(now))
        {
            _logger.LogDebug("Refreshing reminder batch (current count: {Count}, lastDue: {LastDue}, now: {Now})", _window.Count, _window.LastDueTime?.ToString("O") ?? "<none>", now.ToString("O"));
            await LoadBatchAsync(ct);
        }
    }
}
