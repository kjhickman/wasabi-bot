using MassTransit;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Consumers;

public class InteractionReceivedConsumer : IConsumer<InteractionReceivedMessage>
{
    private readonly InteractionRecordService _interactionRecordService;
    private readonly Tracer _tracer;
    private readonly ILogger<InteractionReceivedConsumer> _logger;

    public InteractionReceivedConsumer(InteractionRecordService interactionRecordService, Tracer tracer, ILogger<InteractionReceivedConsumer> logger)
    {
        _interactionRecordService = interactionRecordService;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InteractionReceivedMessage> context)
    {
        _logger.LogInformation("Received interaction: {InteractionId}", context.Message.Id);
        using var span = _tracer.StartActiveSpan("consumer.interaction_received");
        var result = InteractionRecord.Create(context.Message);
        await _interactionRecordService.CreateAsync(result);
    }
}