# WasabiBot

A Discord bot with AI-powered features built on .NET 10 and .NET Aspire.

## Tech Stack

- **Runtime**: .NET 10, .NET Aspire 13.1
- **Discord**: NetCord
- **Database**: PostgreSQL with Entity Framework Core
- **AI/LLM**: Microsoft.Extensions.AI with Gemini (default) and Grok (image captions)
- **Testing**: TUnit, NSubstitute, Testcontainers

## Project Structure

| Project | Purpose |
|---------|---------|
| `WasabiBot.AppHost` | Aspire orchestrator — configures services, secrets, and dependencies |
| `WasabiBot.Api` | Main app — Discord bot gateway, Blazor UI, REST API |
| `WasabiBot.Api.Generators` | Source generator for command handler registration |
| `WasabiBot.DataAccess` | EF Core DbContext and entities |
| `WasabiBot.Migrations` | Standalone migration runner |
| `WasabiBot.ServiceDefaults` | Shared Aspire defaults (OpenTelemetry, Serilog) |

## Running the Project

```bash
aspire run
```

Secrets (Discord tokens, API keys) are configured in `src/WasabiBot.AppHost/Program.cs`.

## Running Tests

**Important!! This is the only way to run tests with TUnit**
```bash
dotnet run --project test/WasabiBot.UnitTests
```

## Adding Database Migrations

```bash
dotnet ef migrations add <MigrationName> --project src/WasabiBot.DataAccess
```

## Key Patterns

- **Vertical slice architecture**: Features in `src/WasabiBot.Api/Features/` contain commands, services, and contracts together
- **Command handlers**: Use `[CommandHandler]` attribute — see any command in Features/ for examples
- **Keyed AI services**: Multiple LLM providers via `[FromKeyedServices(AIServiceProvider.X)]`
- **Test builders**: Use builder pattern in `test/WasabiBot.UnitTests/Builders/`
- **Infrastructure extensions**: Each Infrastructure/ subfolder has `Extensions.cs` for DI registration

## Detailed Documentation

Read these files when working on specific areas:

| Document | When to read |
|----------|--------------|
| `agent_docs/adding_commands.md` | Adding or modifying Discord slash commands |
| `agent_docs/database_changes.md` | Working with entities, migrations, or data access |
| `agent_docs/infrastructure_aws.md` | Deployment, Terraform, or AWS infrastructure |
