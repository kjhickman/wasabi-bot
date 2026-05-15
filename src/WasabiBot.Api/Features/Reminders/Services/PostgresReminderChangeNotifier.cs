using Dapper;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Infrastructure.Database;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class PostgresReminderChangeNotifier(IDbConnectionFactory connectionFactory, Tracer tracer) : IReminderChangeNotifier
{
    private const string ChannelName = "reminders_changed";

    public async Task NotifyReminderChangedAsync(CancellationToken ct = default)
    {
        using var span = tracer.StartActiveSpan("reminder.change-notify");
        span.SetAttribute("messaging.destination.name", ChannelName);

        using var connection = await connectionFactory.CreateConnection(ct);
        await connection.ExecuteAsync($"SELECT pg_notify('{ChannelName}', '')");
    }
}
