# Database Changes

Read this when working with database entities, migrations, or data access.

## Project Structure

- `src/WasabiBot.DataAccess/` — EF Core DbContext and data access layer
- `src/WasabiBot.DataAccess/Entities/` — Entity classes
- `src/WasabiBot.DataAccess/Migrations/` — EF Core migrations
- `src/WasabiBot.Migrations/` — Standalone migration runner for deployment

## Adding a New Entity

1. Create entity class in `src/WasabiBot.DataAccess/Entities/`
2. Add `DbSet<T>` property to `WasabiBotContext`
3. Configure indexes/constraints in `OnModelCreating` if needed
4. Generate migration

## Example: Entity

See `src/WasabiBot.DataAccess/Entities/ReminderEntity.cs`:

```csharp
public class ReminderEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ChannelId { get; set; }
    public required string ReminderMessage { get; set; }
    public DateTimeOffset RemindAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsReminderSent { get; set; }
}
```

## DbContext Configuration

See `src/WasabiBot.DataAccess/WasabiBotContext.cs`:

- Lines 8-9: `DbSet<T>` properties expose entity tables
- Lines 13-24: `OnModelCreating` configures indexes, filters, and column types (e.g., `jsonb`)

## Generating Migrations

```bash
dotnet ef migrations add <MigrationName> --project src/WasabiBot.DataAccess
```

Migrations are applied automatically by `WasabiBot.Migrations` during deployment.

## Data Access Services

Repository/service classes go in `src/WasabiBot.DataAccess/Services/`. Register them via extension methods following the pattern in other Infrastructure folders.

## Checklist

- [ ] Create entity in `Entities/`
- [ ] Add `DbSet<T>` to `WasabiBotContext`
- [ ] Configure indexes in `OnModelCreating` if needed
- [ ] Generate migration with `dotnet ef migrations add`
- [ ] Add repository/service if complex queries are needed
