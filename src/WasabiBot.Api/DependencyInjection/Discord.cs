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
    }

    public static void MapDiscordCommands(this WebApplication app)
    {
        app.AddSlashCommand(MagicConch.CommandName, MagicConch.CommandDescription, MagicConch.Command);
        app.AddSlashCommand(CaptionThis.CommandName, CaptionThis.CommandDescription, CaptionThis.Command);
    }
}
