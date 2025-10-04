using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public sealed class InteractionService(WasabiBotContext context) : IInteractionService
{
    public async Task<InteractionEntity?> GetByIdAsync(long id)
    {
        return await context.Interactions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<bool> CreateAsync(InteractionEntity interaction)
    {
        context.Interactions.Add(interaction);
        var saved = await context.SaveChangesAsync();
        return saved > 0;
    }
}
