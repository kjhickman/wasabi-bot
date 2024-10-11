using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Interfaces;
using WasabiBot.Messaging.Messages;

namespace WasabiBot.Messaging.Handlers;

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
