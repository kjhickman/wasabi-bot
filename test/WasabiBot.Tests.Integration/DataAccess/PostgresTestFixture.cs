using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Trace;
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
    }
    
    public async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresTestCollectionFixture))]
public class PostgresTestCollectionFixture :ICollectionFixture<PostgresTestFixture> { }