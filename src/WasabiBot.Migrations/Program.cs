using Microsoft.Extensions.Configuration;
using WasabiBot.Migrations;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("wasabi-db");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'wasabi-db' not found.");
}

MigrationRunner.Run(connectionString);




