using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using WasabiBot.Discord;
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

    private readonly string _publicKey;
    
    // Needed by tests to sign simulated discord requests
    public string PrivateKey { get; }

    public WasabiBotApiFactory()
    {
        var (privateKey, publicKey) = SignatureUtility.GenerateKeyPair();
        _publicKey = publicKey;
        PrivateKey = privateKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbConnection>();
            services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(_dbContainer.GetConnectionString()));

            services.RemoveAll<IOptions<DiscordSettings>>();
            services.Configure<DiscordSettings>(options =>
            {
                options.PublicKey = _publicKey;
            });
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

[CollectionDefinition(nameof(WasabiBotApiFactory))]
public class WasabiBotApiFactoryCollectionFixture : ICollectionFixture<WasabiBotApiFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}