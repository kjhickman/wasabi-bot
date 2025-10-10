using NetCord.Rest;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class ReminderProcessor : BackgroundService
{
    private readonly ILogger<ReminderProcessor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public ReminderProcessor(ILogger<ReminderProcessor> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderDispatcher started (interval: {IntervalSeconds}s)", Interval.TotalSeconds);

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await DispatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // Graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while dispatching reminders batch");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        _logger.LogInformation("ReminderDispatcher stopping");
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var discordClient = scope.ServiceProvider.GetRequiredService<RestClient>();

        var dueReminders = await reminderService.GetDueAsync(DateTimeOffset.UtcNow, cancellationToken: ct);
        if (dueReminders.Count == 0)
        {
            return;
        }

        var tracer = scope.ServiceProvider.GetRequiredService<Tracer>();
        using var span = tracer.StartActiveSpan($"{nameof(ReminderProcessor)}.{nameof(DispatchAsync)}");
        _logger.LogInformation("Dispatching {Count} reminder(s)", dueReminders.Count);
        var dispatchedIds = new List<long>(dueReminders.Count);

        foreach (var reminder in dueReminders)
        {
            try
            {
                await discordClient.SendMessageAsync((ulong)reminder.ChannelId,
                    $"<@{reminder.UserId}> Reminder: {reminder.ReminderMessage}", cancellationToken: ct);
                dispatchedIds.Add(reminder.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break; // Stop processing further on cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder {ReminderId} to channel {ChannelId}", reminder.Id, reminder.ChannelId);
            }
        }

        if (dispatchedIds.Count > 0)
        {
            try
            {
                await reminderService.MarkSentAsync(dispatchedIds, ct);
                _logger.LogDebug("Marked {Count} reminder(s) as sent", dispatchedIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} reminder(s) as sent", dispatchedIds.Count);
            }
        }
    }
}
