using WasabiBot.MigrationsRunner;

var connectionString = Environment.GetEnvironmentVariable("PG_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new Exception("Connection string is missing or invalid.");
}

DatabaseUtility.Initialize(connectionString);
