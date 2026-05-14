using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WasabiBot.Api.Persistence;

public sealed class WasabiBotContextFactory : IDesignTimeDbContextFactory<WasabiBotContext>
{
    public WasabiBotContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__wasabibot-db")
            ?? "Host=localhost;Database=wasabibot;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<WasabiBotContext>();
        builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("WasabiBot.Migrations"));

        return new WasabiBotContext(builder.Options);
    }
}
