using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess.Interfaces;

public interface IInteractionRepository
{
    Task<InteractionEntity?> GetByIdAsync(ulong id);
    Task<bool> CreateAsync(InteractionEntity interaction);
}
