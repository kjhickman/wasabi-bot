using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess.Abstractions;

public interface IInteractionService
{
    Task<InteractionEntity?> GetByIdAsync(long id);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
