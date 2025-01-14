using Discord.Interactions;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using WasabiBot.Discord.Modules;

namespace WasabiBot.Discord;

public static class DependencyInjection
{
    public static IServiceCollection AddDiscord(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DiscordRestClient>();
        var config = new InteractionServiceConfig
        {
            UseCompiledLambda = true,
            DefaultRunMode = RunMode.Sync
        };
        services.AddSingleton(config);
        services.AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordRestClient>()));
        services.Configure<DiscordSettings>(configuration.GetSection("Discord"));
        return services;
    }
    
    public static async Task InitializeDiscordAsync(this IServiceProvider provider)
    {
        // Log client in
        // var discord = provider.GetRequiredService<DiscordRestClient>();
        // var settings = provider.GetRequiredService<IOptions<DiscordSettings>>().Value;
        // await discord.LoginAsync(TokenType.Bot, settings.Token);
        
        // Add commands to the interaction service
        var interactions = provider.GetRequiredService<InteractionService>();
        await interactions.AddWasabiBotModules(provider);
    }

    public static async Task AddWasabiBotModules(this InteractionService interactions, IServiceProvider? provider)
    {
        if (provider is null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(TracerProvider.Default.GetTracer("wasabi-bot"));
            provider = services.BuildServiceProvider();
        }
        
        await interactions.AddModuleAsync<MagicConchModule>(provider);
    }
}