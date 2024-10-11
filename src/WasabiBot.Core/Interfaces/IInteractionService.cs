using WasabiBot.Core.Discord;
using WasabiBot.Core.Models;

namespace WasabiBot.Core.Interfaces;

public interface IInteractionService
{
    Task<Result<InteractionResponse>> HandleInteraction(Interaction interaction);
    Task<Result> HandleDeferredInteraction(Interaction interaction);
}
