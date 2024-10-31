using MassTransit;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Consumers;

public class InteractionDeferredConsumer : IConsumer<InteractionDeferredMessage>
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger<InteractionDeferredConsumer> _logger;
    private readonly Tracer _tracer;

    public InteractionDeferredConsumer(IInteractionService interactionService,
        ILogger<InteractionDeferredConsumer> logger, Tracer tracer)
    {
        _interactionService = interactionService;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task Consume(ConsumeContext<InteractionDeferredMessage> context)
    {
        using var span = _tracer.StartActiveSpan("consumer.interaction_deferred");
        _logger.LogInformation("Handling deferred interaction message");
        try
        {
            await _interactionService.HandleDeferredInteraction(context.Message, context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle deferred interaction");
            throw;
        }
    }
}
