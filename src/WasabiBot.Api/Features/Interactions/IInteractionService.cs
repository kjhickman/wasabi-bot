using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Interactions;

public interface IInteractionService
{
    Task<InteractionEntity?> GetByIdAsync(long id);
    Task<InteractionEntity[]> GetAllAsync(GetAllInteractionsRequest request);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
