using Npgsql;
using WasabiBot.Api.Features.Reminders.Abstractions;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class PostgresReminderChangeNotifier(IConfiguration configuration) : IReminderChangeNotifier
{
    private const string ChannelName = "reminders_changed";
    private readonly string _connectionString = configuration.GetConnectionString("wasabi_db")
                                               ?? throw new InvalidOperationException("Connection string 'wasabi_db' was not found.");

    public async Task NotifyReminderChangedAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_notify('{ChannelName}', '')";
        await command.ExecuteNonQueryAsync(ct);
    }
}
