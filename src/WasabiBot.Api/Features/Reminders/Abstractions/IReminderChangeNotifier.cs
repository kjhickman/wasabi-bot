namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface IReminderChangeNotifier
{
    Task NotifyReminderChangedAsync(CancellationToken ct = default);
}
