using Lavalink4NET;
using Lavalink4NET.NetCord;
using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.OpenApi;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Persistence;
using NetCord.Hosting.Gateway;
using NetCord.Gateway;
using NetCord;
using NetCord.Hosting.Services.ApplicationCommands;

namespace WasabiBot.Api.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WasabiBotContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("wasabi_db"),
                npgsql => npgsql.MigrationsAssembly("WasabiBot.Migrations")));

        builder.Services.AddHybridCache();

        builder.Services.AddOpenApi(OpenApiConfiguration.Build());

        builder.Services.AddDiscordGateway(x =>
        {
            x.Intents = GatewayIntents.AllNonPrivileged;
            x.Presence = new PresenceProperties(UserStatusType.Online)
            {
                Since = DateTimeOffset.UtcNow,
                Activities = [ new UserActivityProperties("custom", UserActivityType.Custom)
                {
                    State = "Type /help for commands"
                }],
                Afk = false
            };
        });
        builder.Services.AddApplicationCommands();

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

        builder.Services.AddOptions<OpenRouterV2Options>()
            .Bind(builder.Configuration.GetSection(OpenRouterV2Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IChatClientFactory, ChatClientFactory>();

        builder.AddAuthServices();
    }
}
