using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDbContext(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WasabiBotContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
        });
    }
}
