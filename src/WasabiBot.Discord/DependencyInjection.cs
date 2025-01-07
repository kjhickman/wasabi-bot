using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WasabiBot.Discord;

public static class DependencyInjection
{
    public static IServiceCollection AddDiscord(this IServiceCollection services, IConfiguration configuration)
    {
        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
        };

        services.AddSingleton(socketConfig);
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<InteractionService>(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
        services.AddSingleton<InteractionHandler>();
        services.AddHostedService<DiscordHostedService>();

        services.Configure<DiscordSettings>(configuration.GetSection("Discord"));
        return services;
    }
}