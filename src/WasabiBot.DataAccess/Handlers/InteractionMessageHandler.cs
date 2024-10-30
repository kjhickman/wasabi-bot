using MassTransit;
using Microsoft.Extensions.Logging;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionMessageHandler : IConsumer<DeferredInteractionMessage>
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger<InteractionMessageHandler> _logger;

    public InteractionMessageHandler(IInteractionService interactionService, ILogger<InteractionMessageHandler> logger)
    {
        _interactionService = interactionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeferredInteractionMessage> context)
    {
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
