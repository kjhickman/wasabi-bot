using FluentResults;
using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IInteractionService
{
    Task<Result<InteractionResponse>> HandleInteraction(Interaction interaction);
    Task<Result> HandleDeferredInteraction(Interaction interaction, CancellationToken ct = default);
}
