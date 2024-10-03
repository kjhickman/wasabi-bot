using VinceBot.Discord;

namespace VinceBot.Services;

public class InteractionService : IInteractionService
{
    public async Task<InteractionResponse> HandleInteraction(Interaction interaction)
    {
        throw new NotImplementedException();
    }
}

public interface IInteractionService
{
    Task<InteractionResponse> HandleInteraction(Interaction interaction);
}
