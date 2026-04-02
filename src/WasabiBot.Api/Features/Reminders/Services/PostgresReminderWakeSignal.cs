using System.Data;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;

namespace WasabiBot.Api.Features.Reminders.Services;

public sealed class PostgresReminderWakeSignal : IReminderWakeSignal, IHostedService, IAsyncDisposable
{
    private const string ChannelName = "reminders_changed";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<PostgresReminderWakeSignal> _logger;
    private readonly Tracer _tracer;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public PostgresReminderWakeSignal(ILogger<PostgresReminderWakeSignal> logger, IConfiguration configuration, Tracer tracer)
    {
        _logger = logger;
        _tracer = tracer;
        _connectionString = configuration.GetConnectionString("wasabi_db")
                            ?? throw new InvalidOperationException("Connection string 'wasabi_db' was not found.");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("reminder.wake-signal.start");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenerTask = Task.Run(() => RunListenerLoopAsync(_cts.Token), CancellationToken.None);
        await _ready.Task.WaitAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("reminder.wake-signal.stop");

        if (_cts == null || _listenerTask == null)
        {
            return;
        }

        await _cts.CancelAsync();

        try
        {
            await _listenerTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task WaitAsync(CancellationToken ct) => _signal.WaitAsync(ct);

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            _cts.Dispose();
        }

        _signal.Dispose();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunListenerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var span = _tracer.StartActiveSpan("reminder.wake-signal.listen");

            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                connection.Notification += OnNotification;

                await connection.OpenAsync(ct);

                await using var command = connection.CreateCommand();
                command.CommandText = $"LISTEN {ChannelName}";
                await command.ExecuteNonQueryAsync(ct);

                _logger.LogInformation("Listening for reminder notifications on channel {Channel}", ChannelName);
                if (!_ready.Task.IsCompleted)
                {
                    _ready.TrySetResult();
                }
                else
                {
                    Signal();
                }

                while (!ct.IsCancellationRequested && connection.State == ConnectionState.Open)
                {
                    await connection.WaitAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (NpgsqlException ex) when (ct.IsCancellationRequested || ex.InnerException is EndOfStreamException)
            {
                span.RecordException(ex);
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogDebug(ex, "Reminder notification listener stream ended; reconnecting in {Delay}", ReconnectDelay);

                try
                {
                    await Task.Delay(ReconnectDelay, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                span.RecordException(ex);
                _logger.LogWarning(ex, "Reminder notification listener disconnected; retrying in {Delay}", ReconnectDelay);

                try
                {
                    await Task.Delay(ReconnectDelay, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        if (e.Channel == ChannelName)
        {
            using var span = _tracer.StartActiveSpan("reminder.wake-signal.notification");
            span.SetAttribute("messaging.destination.name", e.Channel);
            Signal();
        }
    }

    private void Signal()
    {
        if (_signal.CurrentCount != 0)
        {
            return;
        }

        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }
}
