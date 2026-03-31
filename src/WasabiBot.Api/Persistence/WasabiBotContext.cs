using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Persistence;

public sealed class WasabiBotContext(DbContextOptions<WasabiBotContext> options) : DbContext(options)
{
    public DbSet<InteractionEntity> Interactions => Set<InteractionEntity>();
    public DbSet<ReminderEntity> Reminders => Set<ReminderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InteractionEntity>(builder =>
        {
            builder.Property(e => e.Data).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReminderEntity>()
            .ToTable("Reminders", tableBuilder => tableBuilder.ExcludeFromMigrations());
    }
}
