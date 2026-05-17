using Lavalink4NET;
using Lavalink4NET.NetCord;
using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Infrastructure.OpenApi;
using WasabiBot.Api.Persistence;

namespace WasabiBot.Api.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WasabiBotContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("wasabi_db"),
                npgsql => npgsql.MigrationsAssembly("WasabiBot.Migrations")));

        builder.Services.AddHybridCache();

        builder.Services.AddOpenApi(OpenApiConfiguration.Build());

        builder.Services.AddLavalink();
        var lavalinkSection = builder.Configuration.GetSection("Lavalink");
        builder.Services.AddOptions<AudioServiceOptions>()
            .Configure(audioOptions =>
            {
                audioOptions.BaseAddress = new Uri(lavalinkSection["BaseUrl"]!, UriKind.Absolute);
                audioOptions.Passphrase = lavalinkSection["Password"] ?? "youshallnotpass";
                audioOptions.ResumptionOptions = new LavalinkSessionResumptionOptions(
                    TimeSpan.FromSeconds(lavalinkSection.GetValue("ResumeTimeoutSeconds", 60)));
            });
    }
}
