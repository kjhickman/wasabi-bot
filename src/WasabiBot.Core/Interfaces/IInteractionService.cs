using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IInteractionService
{
    Task<InteractionResponse> HandleInteraction(Interaction interaction);
    Task HandleDeferredInteraction(Interaction interaction, CancellationToken ct = default);
}
