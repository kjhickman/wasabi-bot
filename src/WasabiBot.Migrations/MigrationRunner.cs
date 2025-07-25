﻿using System.Reflection;
using DbUp;

namespace WasabiBot.Migrations;

public static class MigrationRunner
{
    public static int Run(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Connection string not found in configuration");
            Console.ResetColor();
            return -1;
        }

        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .WithTransaction()
            .Build();

        if (upgradeEngine.IsUpgradeRequired())
        {
            var result = upgradeEngine.PerformUpgrade();
            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.Error);
                Console.ResetColor();
                return -1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Database migration completed successfully");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("No database migration required");
        return 0;
    }
}
