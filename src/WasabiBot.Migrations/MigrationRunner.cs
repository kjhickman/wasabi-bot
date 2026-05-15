using System.Reflection;
using DbUp;

namespace WasabiBot.Migrations;

public static class MigrationRunner
{
    public static int Run(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Connection string not provided");
            Console.ResetColor();
            return -1;
        }

        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogToConsole()
                .Build();

            var scriptsToExecute = upgrader.GetScriptsToExecute();
            if (scriptsToExecute.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Database is up to date.");
                Console.ResetColor();
                return 0;
            }

            Console.WriteLine($"Applying {scriptsToExecute.Count} migration(s)...");
            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                throw result.Error;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Migrations complete.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Migration failed: " + ex.Message);
            Console.WriteLine(ex);
            Console.ResetColor();
            return -1;
        }
    }
}
