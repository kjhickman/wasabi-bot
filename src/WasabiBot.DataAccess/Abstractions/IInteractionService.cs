using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Abstractions;

public interface IInteractionService
{
    Task<InteractionEntity?> GetByIdAsync(long id);
    Task<InteractionEntity[]> GetAllAsync(GetAllInteractionsRequest request);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
