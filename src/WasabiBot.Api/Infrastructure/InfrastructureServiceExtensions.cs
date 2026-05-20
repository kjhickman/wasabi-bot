using System.ClientModel;
using Lavalink4NET;
using Lavalink4NET.NetCord;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Infrastructure.OpenApi;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Persistence;
using NetCord.Hosting.Gateway;
using NetCord.Gateway;
using NetCord;
using NetCord.Hosting.Services.ApplicationCommands;
using OpenAI;

namespace WasabiBot.Api.Infrastructure;

public static class InfrastructureServiceExtensions
{
    private const string GeminiFlashModel = "gemini-3.5-flash";
    private const string GoogleOpenAiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private const string GoogleApiKeyConfigKey = "GoogleAi:ApiKey";

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

        builder.Services
            .AddChatClient(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var apiKey = configuration[GoogleApiKeyConfigKey]
                    ?? throw new InvalidOperationException("Google AI API key is not configured.");

                var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(GoogleOpenAiEndpoint) };
                return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                    .GetChatClient(GeminiFlashModel)
                    .AsIChatClient();
            })
            .UseOpenTelemetry(sourceName: "Microsoft.Extensions.AI")
            .UseFunctionInvocation()
            .UseLogging();

        builder.Services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var apiKey = configuration[GoogleApiKeyConfigKey]
                ?? throw new InvalidOperationException("Google AI API key is not configured.");

            return new Google.GenAI.Client(apiKey: apiKey);
        });

        builder.AddAuthServices();
    }
}
