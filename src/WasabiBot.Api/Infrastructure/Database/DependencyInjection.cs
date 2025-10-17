using Microsoft.EntityFrameworkCore;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using WasabiBot.DataAccess;

namespace WasabiBot.Api.Infrastructure.Database;

public static class DependencyInjection
{
    public static void AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WasabiBotContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
        });

        builder.Services.AddTickerQ(o =>
        {
            o.SetMaxConcurrency(0); // Will use Environment.ProcessorCount
            o.AddOperationalStore<WasabiBotContext>(ef =>
            {
                ef.UseModelCustomizerForMigrations();
            });
        });
    }
}
