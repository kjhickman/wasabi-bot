# Repository Guidelines

## Project Structure & Module Organization
The solution is split under `src/` and `test/`. `src/WasabiBot.Api` hosts the HTTP facade and Discord slash command handlers; features live under `Features/*`, shared helpers under `Core/` and `Infrastructure/`. `src/WasabiBot.AppHost` is the .NET Aspire orchestrator that boots Postgres and wires the API plus migrations for local dev. Persistence code lives in `src/WasabiBot.DataAccess` with EF Core services, while `src/WasabiBot.Migrations` exposes a CLI runner for schema updates. Reusable hosting defaults are collected in `src/WasabiBot.ServiceDefaults`. Tests mirror production code: fast unit specs in `test/WasabiBot.UnitTests` and database-backed flows in `test/WasabiBot.IntegrationTests`.

## Build, Test, and Development Commands
- `dotnet restore` hydrates NuGet packages after dependency changes.
- `dotnet build` compiles the project and surfaces analyzer warnings.
- `dotnet test test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj --no-build` covers the fast suite; add `test/WasabiBot.IntegrationTests` (Docker required) when persistence or reminders change.

## Coding Style & Naming Conventions
TBD

## Testing Guidelines
xUnit v3 drives both unit and integration suites. Name files `<TypeUnderTest>Tests.cs` and align namespaces with the implementation. Prefer `[Theory]` for data-driven cases and exercise boundary paths (see `test/WasabiBot.UnitTests/ReminderTimeCalculatorTests.cs`). Integration tests spin up PostgreSQL through testcontainers (`TestFixture`); ensure Docker Desktop is running and call `await fixture.ResetDatabase()` before asserting on mutated state. New Discord features should ship with at least one unit spec plus, when stateful, an integration test against `WasabiBotContext`.

## Commit Guidelines
Commits follow Conventional Commits.

### Quick examples
* `feat: new feature`
* `fix(scope): bug in scope`
* `feat!: breaking change` / `feat(scope)!: rework API`
* `chore(deps): update dependencies`

### Commit types
* `build`: Changes that affect the build system or external dependencies (example scopes: gulp, broccoli, npm)
* `ci`: Changes to CI configuration files and scripts (example scopes: Travis, Circle, BrowserStack, SauceLabs)
* **`chore`: Changes which doesn't change source code or tests e.g. changes to the build process, auxiliary tools, libraries**
* `docs`: Documentation only changes
* **`feat`: A new feature**
* **`fix`: A bug fix**
* `perf`: A code change that improves performance
* `refactor`:  A code change that neither fixes a bug nor adds a feature
* `revert`: Revert something
* `style`: Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
* `test`: Adding missing tests or correcting existing tests
