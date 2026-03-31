namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface IReminderWakeSignal
{
    Task WaitAsync(CancellationToken ct);
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct);
}
