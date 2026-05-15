using Npgsql;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDatabase(this IHostApplicationBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var connectionString = builder.Configuration.GetConnectionString("wasabi_db")
                ?? throw new InvalidOperationException("Connection string 'wasabi_db' was not found.");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;

            return dataSourceBuilder.Build();
        });
        builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
    }
}
