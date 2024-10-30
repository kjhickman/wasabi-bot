using MassTransit;
using Microsoft.Extensions.Logging;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Consumers;

public class InteractionMessageConsumer : IConsumer<DeferredInteractionMessage>
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger<InteractionMessageConsumer> _logger;

    public InteractionMessageConsumer(IInteractionService interactionService, ILogger<InteractionMessageConsumer> logger)
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
