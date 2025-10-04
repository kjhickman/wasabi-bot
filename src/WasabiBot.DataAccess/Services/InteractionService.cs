using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public sealed class InteractionService(WasabiBotContext context, Tracer tracer) : IInteractionService
{
    public async Task<InteractionEntity?> GetByIdAsync(long id)
    {
        using var span = tracer.StartActiveSpan($"{nameof(InteractionService)}.{nameof(GetByIdAsync)}");
        return await context.Interactions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<bool> CreateAsync(InteractionEntity interaction)
    {
        using var span = tracer.StartActiveSpan($"{nameof(InteractionService)}.{nameof(CreateAsync)}");
        context.Interactions.Add(interaction);
        var saved = await context.SaveChangesAsync();
        return saved > 0;
    }
}
