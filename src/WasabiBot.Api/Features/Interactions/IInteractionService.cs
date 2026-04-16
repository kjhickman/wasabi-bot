using WasabiBot.Api.Infrastructure.Database.Entities;

namespace WasabiBot.Api.Features.Interactions;

public interface IInteractionService
{
    Task<InteractionEntity?> GetByIdAsync(long id);
    Task<InteractionEntity[]> GetAllAsync(GetAllInteractionsRequest request);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
