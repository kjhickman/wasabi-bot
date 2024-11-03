using Microsoft.Extensions.Configuration;
using WasabiBot.MigrationsRunner;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connectionString = configuration.GetConnectionString("wasabiBotDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new Exception($"Connection string is missing or invalid.");
}

DatabaseUtility.Initialize(connectionString);
