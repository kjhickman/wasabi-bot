using WasabiBot.Discord;

namespace WasabiBot.Interfaces;

public interface IInteractionService
{
    Task<InteractionResponse?> HandleInteraction(Interaction interaction);
    Task HandleDeferredInteraction(Interaction interaction);
}
