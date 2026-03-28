using Microsoft.Extensions.Configuration;
using WasabiBot.Migrations;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("wasabi_db");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'wasabi_db' not found.");
}

return MigrationRunner.Run(connectionString);

