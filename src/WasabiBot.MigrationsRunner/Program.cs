using System.Reflection;
using DbUp;
using dotenv.net;

DotEnv.Load();
// DotEnv.Load(new DotEnvOptions(envFilePaths: ["../../.env"]));
var connectionString = Environment.GetEnvironmentVariable("NEON_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Connection string is missing");
    return -1;
}

EnsureDatabase.For.PostgresqlDatabase(connectionString);
var upgradeEngine = DeployChanges.To.PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

if (!upgradeEngine.IsUpgradeRequired())
{
    Console.WriteLine("No upgrade required");
    return 0;
}

var result = upgradeEngine.PerformUpgrade();

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(result.Error);
    Console.ResetColor();
#if DEBUG
    Console.ReadLine();
#endif                
    return -1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Success!");
Console.ResetColor();
return 0;
