using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class PostgresReminderChangeNotifier(NpgsqlDataSource dataSource, Tracer tracer) : IReminderChangeNotifier
{
    private const string ChannelName = "reminders_changed";

    public async Task NotifyReminderChangedAsync(CancellationToken ct = default)
    {
        using var span = tracer.StartActiveSpan("reminder.change-notify");
        span.SetAttribute("messaging.destination.name", ChannelName);

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_notify('{ChannelName}', '')";
        await command.ExecuteNonQueryAsync(ct);
    }
}
