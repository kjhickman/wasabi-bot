using VinceBot.Discord;

namespace VinceBot.Interfaces;

public interface IInteractionService
{
    Task<InteractionResponse> HandleInteraction(Interaction interaction);
}
