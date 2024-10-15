using dotenv.net;
using WasabiBot.MigrationsRunner;

DotEnv.Load();
var connectionString = Environment.GetEnvironmentVariable("NEON_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Connection string is missing");
    return;
}

DatabaseUtility.Initialize(connectionString);


