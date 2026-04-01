# Wasabi Bot

Wasabi Bot is a .NET 10 Discord bot built with NetCord. Keep this file short and use it as onboarding context; read the referenced files only when they are relevant to the task.

## What This Repo Contains

- `src/WasabiBot.Api`: main ASP.NET Core app for the bot and API surface
- `src/WasabiBot.Migrations`: EF Core migrations and database migration runner
- `src/WasabiBot.ServiceDefaults`: shared Aspire, telemetry, and service wiring
- `src/WasabiBot.Api.Generators`: source generators used by the API project
- `test/WasabiBot.UnitTests`, `test/WasabiBot.IntegrationTests`, `test/WasabiBot.TestShared`: automated tests and shared test helpers

## How To Work In This Repo

- Prerequisites: .NET 10 SDK and Aspire CLI 13
- Restore dependencies with `dotnet restore WasabiBot.slnx`
- Start the app from the repo root with `aspire start`
- Use the `aspire` skill when running or debugging the local distributed app
- Use the `discord-playwright-testing` skill when verifying bot behavior through Discord web with `playwright-cli`
- The local app topology lives in `AppHost.cs`; it provisions PostgreSQL and wires Discord/OpenRouter parameters into the API
- Run unit tests with `dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -c Release --no-restore`
- Run integration tests with `dotnet run --project test/WasabiBot.IntegrationTests/WasabiBot.IntegrationTests.csproj -c Release --no-restore`

## Where To Look Next

- `README.md`: local setup and developer expectations
- `AppHost.cs`: Aspire resources, parameters, and local orchestration
- `.github/workflows/tests.yml`: CI validation commands
