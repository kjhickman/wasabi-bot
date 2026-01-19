using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using WasabiBot.DataAccess;

namespace WasabiBot.IntegrationTests.Infrastructure;

/// <summary>
/// Manages PostgreSQL testcontainer lifecycle, migrations, and database resets for integration tests.
/// 
/// Pattern:
/// 1. Container starts once per test (via InitializeAsync)
/// 2. Migrations are applied automatically
/// 3. Respawn resets database state between tests (faster than recreating container)
/// </summary>
public sealed class PostgresTestFixture
{
    private PostgreSqlContainer? _container;
    private Respawner? _respawner;
    private NpgsqlConnection? _respawnConnection;

    /// <summary>Gets the connection string for the running PostgreSQL container.</summary>
    public string ConnectionString => _container?.GetConnectionString()
                                       ?? throw new InvalidOperationException("Container not initialized");

    /// <summary>Creates a new WasabiBotContext connected to the test container.</summary>
    public WasabiBotContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WasabiBotContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new WasabiBotContext(options);
    }

    /// <summary>Initializes the container and applies migrations.</summary>
    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithPassword("TestPassword123!")
            .WithUsername("test")
            .WithDatabase("wasabi_bot_test")
            .Build();

        await _container.StartAsync();

        // Apply migrations
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Initialize Respawner for database resets with explicit PostgreSQL connection
        _respawnConnection = new NpgsqlConnection(ConnectionString);
        await _respawnConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions { DbAdapter = DbAdapter.Postgres });
    }

    /// <summary>Resets all tables in the database while maintaining schema and migrations.</summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner == null || _respawnConnection == null)
            throw new InvalidOperationException("Respawner not initialized");

        // Ensure connection is open before reset
        if (_respawnConnection.State != System.Data.ConnectionState.Open)
        {
            await _respawnConnection.OpenAsync();
        }

        await _respawner.ResetAsync(_respawnConnection);
    }

    /// <summary>Cleans up the container.</summary>
    public async Task DisposeAsync()
    {
        if (_respawnConnection != null)
        {
            await _respawnConnection.CloseAsync();
            await _respawnConnection.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}
