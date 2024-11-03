using System.Reflection;
using DbUp;

namespace WasabiBot.MigrationsRunner;

public static class DatabaseUtility
{
    public static void Initialize(string connectionString)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        var upgradeEngine = DeployChanges.To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        if (!upgradeEngine.IsUpgradeRequired())
        {
            Console.WriteLine("No upgrade required");
            return;
        }

        upgradeEngine.PerformUpgrade();
    }
}