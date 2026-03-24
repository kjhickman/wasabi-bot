using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDbContext(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WasabiBotContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("wasabi-db")
                                   ?? builder.Configuration.GetConnectionString("wasabi_db");

            options.UseNpgsql(connectionString);
        });
    }
}
