using System.Reflection;
using DbUp;

namespace WasabiBot.MigrationsRunner;

public static class DatabaseUtility
{
    public static int Initialize(string connectionString)
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
            return 0;
        }

        var result = upgradeEngine.PerformUpgrade();

        if (!result.Successful)
        {
            return -1;
        }

        return 0;
    }
}