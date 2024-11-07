using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionReceivedHandler : IMessageHandler<InteractionReceivedMessage>
{
    private readonly InteractionRecordService _interactionRecordService;
    private readonly Tracer _tracer;
    private readonly ILogger<InteractionReceivedHandler> _logger;

    public InteractionReceivedHandler(InteractionRecordService interactionRecordService, Tracer tracer, ILogger<InteractionReceivedHandler> logger)
    {
        _interactionRecordService = interactionRecordService;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task HandleAsync(InteractionReceivedMessage message, CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(InteractionReceivedHandler)}.{nameof(HandleAsync)}");
        _logger.LogInformation("Received interaction: {InteractionId}", message.Id);
        var result = InteractionRecord.Create(message);
        await _interactionRecordService.CreateAsync(result);
    }
}
