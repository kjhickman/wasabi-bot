using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public class SqsEndpoint<TMessage> : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IServiceProvider _serviceProvider;
    private readonly ReceiveMessageRequest _request;
    private readonly JsonTypeInfo<TMessage> _jsonTypeInfo;
    private readonly ILogger<SqsEndpoint<TMessage>> _logger;
    private readonly Tracer _tracer;

    public SqsEndpoint(IAmazonSQS sqs, IServiceProvider serviceProvider, ILogger<SqsEndpoint<TMessage>> logger, Tracer tracer)
    {
        _sqs = sqs;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tracer = tracer;

        var queueUrl = QueueInfo.UrlMap[typeof(TMessage).Name];
        _request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 20
        };

        _jsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(typeof(TMessage)) as JsonTypeInfo<TMessage> ??
                        throw new Exception("Missing JsonTypeInfo: " + typeof(TMessage).Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollSqsQueue(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error polling messages");
            }
        }
    }

    private async Task PollSqsQueue(CancellationToken stoppingToken)
    {
        // todo: 2 threads
        var response = await _sqs.ReceiveMessageAsync(_request, stoppingToken);
        if (response?.Messages is null || response.Messages.Count == 0)
        {
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = response.Messages.Count,
            CancellationToken = stoppingToken
        };

        using var span = _tracer.StartActiveSpan($"{nameof(SqsEndpoint<TMessage>)}.{typeof(TMessage).Name}.ProcessBatch");
        var successfulMessages = new ConcurrentBag<Message>();
        _logger.LogInformation("Processing {MessageCount} messages", response.Messages.Count);
        await Parallel.ForEachAsync(response.Messages, parallelOptions, async (message, ct) =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var subscriber = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
                var t = JsonSerializer.Deserialize(message.Body, _jsonTypeInfo) ??
                        throw new Exception("Failed to deserialize message: " + message.Body); // todo: improve error
                await subscriber.HandleAsync(t, ct);
                successfulMessages.Add(message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process message: {MessageId}", message.MessageId);
            }
        });


        var deleteRequest = new DeleteMessageBatchRequest
        {
            QueueUrl = _request.QueueUrl,
            Entries = successfulMessages.Select(m => new DeleteMessageBatchRequestEntry
            {
                Id = m.MessageId,
                ReceiptHandle = m.ReceiptHandle
            }).ToList()
        };

        await _sqs.DeleteMessageBatchAsync(deleteRequest, stoppingToken);
    }
}