using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionReceivedConsumer : IMessageHandler<InteractionReceivedMessage>
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

    public async Task HandleAsync(InteractionReceivedMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received interaction: {InteractionId}", message.Id);
        using var span = _tracer.StartActiveSpan("consumer.interaction_received");
        var result = InteractionRecord.Create(message);
        await _interactionRecordService.CreateAsync(result);
    }
}
