using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using WasabiBot.MigrationsRunner;

namespace WasabiBot.Tests.Integration.Web;

public class WasabiBotApiFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder()
            .WithDatabase("wasabi-bot")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbConnection>();
            services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(_dbContainer.GetConnectionString()));
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
        DatabaseUtility.Initialize(_dbContainer.GetConnectionString());
    }
    
    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}