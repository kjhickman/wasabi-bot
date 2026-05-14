using Microsoft.EntityFrameworkCore;
using Npgsql;
using WasabiBot.Api.Persistence;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDbContext(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var connectionString = builder.Configuration.GetConnectionString("wasabi_db")
                ?? throw new InvalidOperationException("Connection string 'wasabi_db' was not found.");

            return NpgsqlDataSource.Create(connectionString);
        });

        builder.Services.AddDbContext<WasabiBotContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("wasabi_db"),
                npgsql => npgsql.MigrationsAssembly("WasabiBot.Migrations")));
    }
}
