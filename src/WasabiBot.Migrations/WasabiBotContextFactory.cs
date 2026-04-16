using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WasabiBot.Api.Infrastructure.Database;

namespace WasabiBot.Migrations;

public sealed class WasabiBotContextFactory : IDesignTimeDbContextFactory<WasabiBotContext>
{
    public WasabiBotContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__wasabi_db")
            ?? "Host=localhost;Port=5432;Database=wasabi_db;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<WasabiBotContext>();
        builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("WasabiBot.Migrations"));
        return new WasabiBotContext(builder.Options);
    }
}
