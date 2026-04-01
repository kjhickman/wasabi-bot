using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Persistence;

public sealed class WasabiBotContext(DbContextOptions<WasabiBotContext> options) : DbContext(options)
{
    public DbSet<ApiCredentialEntity> ApiCredentials => Set<ApiCredentialEntity>();
    public DbSet<InteractionEntity> Interactions => Set<InteractionEntity>();
    public DbSet<ReminderEntity> Reminders => Set<ReminderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InteractionEntity>(builder =>
        {
            builder.Property(e => e.Data).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ApiCredentialEntity>(builder =>
        {
            builder.ToTable("ApiCredentials");
            builder.Property(e => e.Name).HasColumnType("text");
            builder.Property(e => e.ClientId).HasColumnType("text");
            builder.Property(e => e.SecretHash).HasColumnType("text");
            builder.HasIndex(e => e.ClientId).IsUnique();
            builder.HasIndex(e => e.OwnerDiscordUserId);
        });

        modelBuilder.Entity<ReminderEntity>(builder =>
        {
            builder.ToTable("Reminders");
            builder.Property(e => e.ReminderMessage).HasColumnType("text");
            builder.Property(e => e.Status).HasConversion<string>();
            builder.Property(e => e.LastError).HasColumnType("text");
            builder.HasIndex(e => new { e.Status, e.DueAt });
            builder.HasIndex(e => new { e.UserId, e.Status, e.DueAt });
        });
    }
}
