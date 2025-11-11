using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess.Services;

public sealed class InteractionService(WasabiBotContext context, Tracer tracer) : IInteractionService
{
    public async Task<InteractionEntity?> GetByIdAsync(long id)
    {
        using var span = tracer.StartActiveSpan("interaction.get");
        return await context.Interactions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<InteractionEntity[]> GetAllAsync(GetAllInteractionsRequest request)
    {
        using var span = tracer.StartActiveSpan("interaction.getAll");
        var query = context.Interactions.AsNoTracking().AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(i => i.UserId == request.UserId.Value);

        if (request.ChannelId.HasValue)
            query = query.Where(i => i.ChannelId == request.ChannelId.Value);

        if (request.ApplicationId.HasValue)
            query = query.Where(i => i.ApplicationId == request.ApplicationId.Value);

        if (request.GuildId.HasValue)
            query = query.Where(i => i.GuildId == request.GuildId.Value);

        return await query.ToArrayAsync();

    }

    public async Task<bool> CreateAsync(InteractionEntity interaction)
    {
        using var span = tracer.StartActiveSpan("interaction.create");
        context.Interactions.Add(interaction);
        var saved = await context.SaveChangesAsync();
        return saved > 0;
    }
}

public class GetAllInteractionsRequest
{
    public long? UserId { get; set; }
    public long? ChannelId { get; set; }
    public long? ApplicationId { get; set; }
    public long? GuildId { get; set; }
}
