using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.EventHandlers;
using WasabiBot.Api.Modules;
using WasabiBot.Api.Services;

namespace Microsoft.Extensions.DependencyInjection;

internal static class Discord
{
    public static void AddDiscord(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
        services.AddGatewayHandler<InteractionCreatedEventHandler>();

        // Register commands
        services.AddSingleton<ISlashCommand, MagicConchCommand>();
        services.AddSingleton<ISlashCommand, CaptionThisCommand>();
        services.AddSingleton<ISlashCommand, RemindMeCommand>();

        // Register discord-related services
        services.AddSingleton<INaturalLanguageTimeResolver, NaturalLanguageTimeResolver>();
        services.AddSingleton<NaturalLanguageTimeToolProvider>();
    }

    public static void MapDiscordCommands(this WebApplication app)
    {
        var commands = app.Services.GetServices<ISlashCommand>();
        foreach (var command in commands)
        {
            command.Register(app);
        }
    }
}
