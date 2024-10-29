using dotenv.net;
using Microsoft.Extensions.Configuration;
using WasabiBot.MigrationsRunner;

DotEnv.Load();
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("wasabiBotDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    return -1;
}

return DatabaseUtility.Initialize(connectionString);
