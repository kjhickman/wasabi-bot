# WasabiBot.Migrations

Use this project to apply DbUp SQL migrations.

## Add a migration

Add a new SQL script under `Scripts/` using the next numeric prefix:

```text
Scripts/003_DescribeChange.sql
```

Scripts are embedded into the migration executable and applied in filename order.

## Apply migrations

Run the migration runner:

```bash
dotnet run --project src/WasabiBot.Migrations/WasabiBot.Migrations.csproj
```
