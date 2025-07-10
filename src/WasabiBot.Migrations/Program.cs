using Microsoft.Extensions.Configuration;
using WasabiBot.Migrations;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("wasabi-db")
    ?? throw new InvalidOperationException("Connection string 'wasabi-db' not found in configuration.");;

MigrationRunner.Run(connectionString);




