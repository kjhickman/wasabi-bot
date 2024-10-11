using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionMessageHandler : IMessageHandler<DeferredInteractionMessage>
{
    private readonly IInteractionService _interactionService;

    public InteractionMessageHandler(IInteractionService interactionService)
    {
        _interactionService = interactionService;
    }
    
    public async Task<Result> Handle(IMessage message, CancellationToken ct = default)
    {
        if (message is not Interaction interaction)
        {
            return Result.Fail("Couldn't cast message to interaction");
        }

        return await _interactionService.HandleDeferredInteraction(interaction);
    }
}
