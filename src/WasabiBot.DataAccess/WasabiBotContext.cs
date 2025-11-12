using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.DataAccess;

public sealed class WasabiBotContext(DbContextOptions<WasabiBotContext> options) : DbContext(options)
{
    public DbSet<InteractionEntity> Interactions => Set<InteractionEntity>();
    public DbSet<ReminderEntity> Reminders => Set<ReminderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InteractionEntity>(builder =>
        {
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.GuildId).HasFilter("\"GuildId\" IS NOT NULL");
            builder.Property(e => e.Data).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReminderEntity>(builder =>
        {
            builder.HasIndex(r => r.RemindAt).HasFilter("\"IsReminderSent\" = FALSE");
        });
    }
}

// Adding a new migration:
// dotnet ef migrations add <MigrationName> --project src/WasabiBot.DataAccess
