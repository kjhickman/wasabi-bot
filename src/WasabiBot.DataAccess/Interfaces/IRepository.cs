using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess.Interfaces;

public interface IInteractionRepository
{
    Task<InteractionEntity?> GetByIdAsync(long id);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
