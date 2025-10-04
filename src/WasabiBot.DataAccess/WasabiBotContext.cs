using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess;

public sealed class WasabiBotContext(DbContextOptions<WasabiBotContext> options) : DbContext(options)
{
    public DbSet<InteractionEntity> Interactions => Set<InteractionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InteractionEntity>(builder =>
        {
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.GuildId).HasFilter("\"GuildId\" IS NOT NULL");
        });
    }
}
