using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.EventHandlers;
using WasabiBot.Api.Modules;

namespace Microsoft.Extensions.DependencyInjection;

internal static class Discord
{
    public static void AddDiscord(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
        services.AddGatewayHandler<InteractionCreatedEventHandler>();
        services.AddSingleton<ISlashCommand, MagicConchCommand>();
        services.AddSingleton<ISlashCommand, CaptionThisCommand>();
        services.AddSingleton<ISlashCommand, RemindMeCommand>();
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
