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

        if (request.Cursor != null)
        {
            if (request.SortDirection == SortDirection.Descending)
            {
                query = query.Where(i =>
                    i.CreatedAt < request.Cursor.CreatedAt ||
                    (i.CreatedAt == request.Cursor.CreatedAt && i.Id < request.Cursor.Id));
            }
            else
            {
                query = query.Where(i =>
                    i.CreatedAt > request.Cursor.CreatedAt ||
                    (i.CreatedAt == request.Cursor.CreatedAt && i.Id > request.Cursor.Id));
            }
        }

        query = request.SortDirection == SortDirection.Descending
            ? query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            : query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id);

        // Take limit + 1 to determine if there are more results
        query = query.Take(request.Limit + 1);

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
    public int Limit { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Descending;
    public InteractionCursor? Cursor { get; set; }
}

public class InteractionCursor
{
    public DateTimeOffset CreatedAt { get; set; }
    public long Id { get; set; }
}

public enum SortDirection
{
    Ascending,
    Descending
}
