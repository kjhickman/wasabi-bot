using WasabiBot.Core;
using WasabiBot.Core.Discord;

namespace WasabiBot.Interfaces;

public interface IInteractionService
{
    Task<Result<InteractionResponse>> HandleInteraction(Interaction interaction);
    Task<Result> HandleDeferredInteraction(Interaction interaction);
}
