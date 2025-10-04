using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WasabiBot.DataAccess;

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
            var services = new ServiceCollection();
            services.AddDbContext<WasabiBotContext>(o => o.UseNpgsql(connectionString));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();

            var isMigrationNeeded = ctx.Database.GetPendingMigrations().Any();
            if (!isMigrationNeeded)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Database is up to date.");
                Console.ResetColor();
                return 0;
            }

            var ts = Stopwatch.GetTimestamp();
            Console.WriteLine("Applying migrations...");
            ctx.Database.Migrate();

            Console.ForegroundColor = ConsoleColor.Green;
            var elapsed = Stopwatch.GetElapsedTime(ts);
            Console.WriteLine($"Migrations complete in {elapsed.TotalMilliseconds}ms.");
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
