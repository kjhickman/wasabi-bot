using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public class InMemoryEndpoint<TMessage> : BackgroundService where TMessage : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _queueUrl;
    private readonly ILogger<InMemoryEndpoint<TMessage>> _logger;
    private readonly PeriodicTimer _timer;
    private readonly Tracer _tracer;

    public InMemoryEndpoint(IServiceProvider serviceProvider, ILogger<InMemoryEndpoint<TMessage>> logger, Tracer tracer)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tracer = tracer;
        _queueUrl = QueueInfo.UrlMap[typeof(TMessage).Name];
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // Poll every second
        
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollQueueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing messages");
            }
        }
    }

    private async Task PollQueueAsync(CancellationToken stoppingToken)
    {
        var messages = InMemoryMessageBus.DequeueMessages<TMessage>(_queueUrl, 10).ToList();
        if (messages.Count == 0)
        {
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = messages.Count,
            CancellationToken = stoppingToken
        };

        using var span = _tracer.StartActiveSpan($"{nameof(InMemoryEndpoint<TMessage>)}.ProcessBatch");
        await Parallel.ForEachAsync(messages, parallelOptions, async (message, ct) =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var subscriber = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
                await subscriber.HandleAsync(message, ct);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process message");
            }
        });
    }
}