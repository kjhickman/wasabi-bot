using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionDeferredHandler : IMessageHandler<InteractionDeferredMessage>
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger<InteractionDeferredHandler> _logger;
    private readonly Tracer _tracer;

    public InteractionDeferredHandler(IInteractionService interactionService,
        ILogger<InteractionDeferredHandler> logger, Tracer tracer)
    {
        _interactionService = interactionService;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task HandleAsync(InteractionDeferredMessage message, CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("consumer.interaction_deferred");
        _logger.LogInformation("Handling deferred interaction message");
        try
        {
            await _interactionService.HandleDeferredInteraction(message, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle deferred interaction");
            throw;
        }
    }
}
