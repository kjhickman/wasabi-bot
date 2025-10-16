using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.Configurations;
using TickerQ.EntityFrameworkCore.Entities;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess;

public sealed class WasabiBotContext(DbContextOptions<WasabiBotContext> options) : DbContext(options)
{
    public DbSet<InteractionEntity> Interactions => Set<InteractionEntity>();

    // TickerQ entities
    // public DbSet<TimeTickerEntity> TimeTickers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InteractionEntity>(builder =>
        {
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.GuildId).HasFilter("\"GuildId\" IS NOT NULL");
        });

        // Apply TickerQ entity configurations explicitly (needed for migrations)
        // Default schema is "ticker"
        // modelBuilder.ApplyConfiguration(new TimeTickerConfigurations());
        // modelBuilder.ApplyConfiguration(new CronTickerConfigurations());
        // modelBuilder.ApplyConfiguration(new CronTickerOccurrenceConfigurations());
    }
}

// Adding a new migration:
// dotnet ef migrations add <MigrationName> --project src/WasabiBot.DataAccess
