using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WasabiBot.DataAccess;

// Provides design-time support for 'dotnet ef' commands.
public sealed class WasabiBotContextFactory : IDesignTimeDbContextFactory<WasabiBotContext>
{
    public WasabiBotContext CreateDbContext(string[] args)
    {
        // Prefer environment variable for CI/local tooling
        var cs = Environment.GetEnvironmentVariable("WASABI_DB_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=wasabi-bot;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<WasabiBotContext>();
        builder.UseNpgsql(cs);
        return new WasabiBotContext(builder.Options);
    }
}

