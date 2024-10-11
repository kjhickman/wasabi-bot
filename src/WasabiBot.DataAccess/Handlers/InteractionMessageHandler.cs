using Serilog;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionMessageHandler : IMessageHandler<DeferredInteractionMessage>
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger _logger;

    public InteractionMessageHandler(IInteractionService interactionService, ILogger logger)
    {
        _interactionService = interactionService;
        _logger = logger;
    }
    
    public async Task<Result> Handle(IMessage message, CancellationToken ct = default)
    {
        _logger.Information("Handling deferred interaction message");
        if (message is not Interaction interaction)
        {
            return Result.Fail("Couldn't cast message to interaction");
        }
        
        return await _interactionService.HandleDeferredInteraction(interaction, ct);
    }
}
