using System.Data;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Trace;
using Respawn;
using Testcontainers.PostgreSql;
using WasabiBot.MigrationsRunner;

namespace WasabiBot.Tests.Integration.DataAccess;

public class PostgresTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder()
            .WithDatabase("wasabi-bot")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    
    private DbConnection _connection = null!;
    private Respawner _respawner = null!;
    
    public IServiceProvider ServiceProvider { get; private set; }

    public PostgresTestFixture()
    {
        var services = new ServiceCollection();
        services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(_dbContainer.GetConnectionString()));
        services.AddSingleton(TracerProvider.Default.GetTracer("wasabi-bot"));
        
        ServiceProvider = services.BuildServiceProvider();
    }
    
    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
        DatabaseUtility.Initialize(_dbContainer.GetConnectionString());
        _connection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }
    
    public async Task ResetDatabase() => await _respawner.ResetAsync(_connection);
    
    public async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresTestFixture))]
public class PostgresTestCollectionFixture : ICollectionFixture<PostgresTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}