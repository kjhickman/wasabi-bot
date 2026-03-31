# WasabiBot.Migrations

Use this project to scaffold and apply EF Core migrations.

## Add a migration

From the repo root:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj \
  --startup-project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj
```

Example:

```bash
dotnet ef migrations add AddInteractionIndexes \
  --project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj \
  --startup-project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj
```

If `dotnet ef` is not installed, run:

```bash
dotnet tool install --global dotnet-ef
```

## Apply migrations

Run the migration runner instead of `dotnet ef database update`:

```bash
dotnet run --project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj
```
