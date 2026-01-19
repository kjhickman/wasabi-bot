# Integration Tests with Testcontainers & Respawn

## Overview

Integration tests for WasabiBot use **Testcontainers** to spin up isolated PostgreSQL 18 containers and **Respawn** to reset database state between tests without recreating containers. This approach balances speed (no container recreation) with isolation (clean state per test).

## Architecture

```
IntegrationTestBase (base class for all integration tests)
    ├── CreateContext() — Creates DbContext pointing to test container
    ├── CreateInteractionEntity(), CreateInteractionEntities() — Test data builders
    ├── CreateReminderEntity() — Test data builder
    └── AssertInteractionExistsAsync(), CountInteractionsAsync() — Assertion helpers

PostgresTestFixture (manages container lifecycle)
    ├── InitializeAsync() — Starts container, applies migrations
    ├── ResetDatabaseAsync() — Uses Respawn to clear data between tests
    └── DisposeAsync() — Stops and removes container
```

## Test Flow

1. **Setup** (`IntegrationTestBase.InitializeAsync()`):
   - Starts PostgreSQL 18 testcontainer
   - Applies all EF Core migrations automatically
   - Initializes Respawn for state resets

2. **Test Execution**:
   - Create a `WasabiBotContext` via `CreateContext()`
   - Instantiate the service under test with the context
   - Perform assertions and database operations

3. **Cleanup** (between tests):
   - `ResetDatabaseAsync()` removes all data but keeps schema
   - Next test starts with clean database

4. **Teardown** (`IntegrationTestBase.DisposeAsync()`):
   - Stops and removes the PostgreSQL container

## Running Tests

### Run all integration tests:
```bash
dotnet test test/WasabiBot.IntegrationTests
```

### Run a specific test class:
```bash
dotnet test test/WasabiBot.IntegrationTests --filter "ClassName=InteractionServiceTests"
```

### Run a specific test:
```bash
dotnet test test/WasabiBot.IntegrationTests --filter "InteractionServiceTests.CreateAsync_ShouldInsertInteractionIntoDatabase"
```

> **Note**: TUnit requires `dotnet test`, not `dotnet run` like some test frameworks.

## Writing New Integration Tests

### 1. Create a test class inheriting from `IntegrationTestBase`:

```csharp
public class MyServiceTests : IntegrationTestBase
{
    [Test]
    public async Task MyTest_ShouldBehaveProperly()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new MyService(context, /* other dependencies */);

        // Act
        var result = await service.DoSomethingAsync();

        // Assert
        Assert.That.IsTrue(result);
    }
}
```

### 2. Use test data builders:

```csharp
var interaction = CreateInteractionEntity(
    id: 123,
    userId: 456,
    username: "TestUser"
);

// Or create multiple:
var interactions = CreateInteractionEntities(count: 5, baseUserId: 100);
```

### 3. Use assertion helpers:

```csharp
await AssertInteractionExistsAsync(123);
var count = await CountInteractionsAsync();
```

## Current Test Coverage

### ✅ InteractionService (`test/WasabiBot.IntegrationTests/Features/DataAccess/InteractionServiceTests.cs`)

Tests core CRUD operations and pagination:
- **CreateAsync**: Insert interactions into database
- **GetByIdAsync**: Retrieve single interaction
- **GetAllAsync**: Query with filters (userId, channelId, applicationId, guildId)
- **Pagination**: Cursor-based pagination with limit
- **Sorting**: Ascending/descending by CreatedAt
- **Combined Filters**: Multiple filter criteria simultaneously

**Why InteractionService?**
- Simple database dependencies (no external services)
- Core to many features (stats, interactions querying)
- Pagination logic is critical and error-prone
- Good foundation for testing other database services

## Recommended Next Tests

### 🟡 StatsService (`src/Features/Stats/StatsService.cs`)
- Tests aggregation queries with JSONB parsing
- Reuses Interaction seeding from InteractionServiceTests
- Complexity: ⭐⭐ (read-only, JSON parsing)

### 🟡 ReminderService (`src/Features/Reminders/Services/ReminderService.cs`)
- Tests multi-step operations (insert, query, bulk update)
- Mocks Discord client dependency
- Complexity: ⭐⭐⭐ (multiple database operations)

### ❌ Skip for Now (unit tests sufficient)
- **TimeParsingService**: No database, external LLM API
- **HttpClientImageRetrievalService**: No database, simple HTTP
- **GetToken**: No database, pure token generation
- **Handlers** (GetInteractions, InteractionCreatedEventHandler): Consider after foundation is proven

## Connection String Configuration

### Test Container (automatic)
The test container uses:
- **Image**: `postgres:18`
- **User**: `test`
- **Password**: `TestPassword123!`
- **Database**: `wasabi_bot_test`

Connection string is automatically generated from the container's exposed port.

### Local/CI Database
If you want to run tests against a local or CI PostgreSQL instance instead of containers, set the environment variable:

```bash
export WASABI_DB_CONNECTION="Host=localhost;Port=5432;Database=wasabi_bot;Username=postgres;Password=postgres"
dotnet test test/WasabiBot.IntegrationTests
```

> **Not recommended** for CI/local dev; use containers to avoid database pollution.

## Respawn Configuration

Respawn resets database state between tests by:
1. **Identifying all tables** in the schema
2. **Deleting all rows** (respecting foreign key constraints)
3. **NOT dropping** the schema or migrations

This is **much faster** than recreating the container (~100ms vs ~10s per test).

### Customizing Respawn Behavior

In `PostgresTestFixture.InitializeAsync()`:

```csharp
_respawner = await Respawner.CreateAsync(
    ConnectionString,
    new RespawnOptions 
    { 
        DbAdapter = DbAdapter.Postgres,
        // Optional: tables to exclude from reset
        // TablesToExclude = new[] { "schema_migrations" }
    }
);
```

## Troubleshooting

### Docker not running
```
Error: Unable to find image `postgres:18`
```
Ensure Docker Desktop or Docker daemon is running:
```bash
docker ps
```

### Port conflicts
If port 5432 is already in use, Testcontainers will use a random available port. Check logs for the actual port.

### Migrations not applied
Ensure [WasabiBot.DataAccess](../../src/WasabiBot.DataAccess) migrations are up-to-date:
```bash
dotnet ef migrations add <MigrationName> --project src/WasabiBot.DataAccess
```

### Test hangs
- Check if container is still running: `docker ps`
- Manually clean up: `docker stop $(docker ps -q) && docker rm $(docker ps -aq)`
- Restart tests: `dotnet test test/WasabiBot.IntegrationTests`

## Performance Notes

- **First test**: ~15-20s (container startup + migrations)
- **Subsequent tests**: ~100-500ms each (Respawn reset)
- **Total suite** (15 tests): ~20-30s

To speed up local iteration, run a single test class:
```bash
dotnet test test/WasabiBot.IntegrationTests --filter "InteractionServiceTests"
```

## References

- [Testcontainers .NET](https://testcontainers.com/docs/languages/dotnet/)
- [Respawn Documentation](https://github.com/jbogard/Respawn)
- [TUnit Test Framework](https://github.com/thomhurst/TUnit)
