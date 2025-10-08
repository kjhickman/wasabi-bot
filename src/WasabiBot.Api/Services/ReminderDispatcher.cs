using NetCord.Rest;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.Services;

public sealed class ReminderDispatcher : BackgroundService
{
    private readonly ILogger<ReminderDispatcher> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public ReminderDispatcher(ILogger<ReminderDispatcher> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderDispatcher started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching reminders");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
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

        foreach (var reminder in dueReminders)
        {
            await discordClient.SendMessageAsync((ulong)reminder.ChannelId,
                $"<@{reminder.UserId}> Reminder: {reminder.ReminderMessage}", cancellationToken: ct);
        }

        await reminderService.MarkSentAsync(dueReminders.Select(r => r.Id), ct);
    }
}
