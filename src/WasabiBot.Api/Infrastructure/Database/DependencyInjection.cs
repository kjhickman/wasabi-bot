using Npgsql;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDbContext(this IHostApplicationBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var connectionString = builder.Configuration.GetConnectionString("wasabi_db")
                ?? throw new InvalidOperationException("Connection string 'wasabi_db' was not found.");

            return NpgsqlDataSource.Create(connectionString);
        });
    }
}
