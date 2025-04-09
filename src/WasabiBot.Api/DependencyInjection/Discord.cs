using System.Reflection;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.EventHandlers;
using WasabiBot.Api.Modules;

namespace WasabiBot.Api.DependencyInjection;

public static class Discord
{
    public static void AddDiscord(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
        services.AddGatewayEventHandler<InteractionCreatedEventHandler>();
    }

    public static void MapDiscordCommands(this WebApplication app)
    {
        var guildId = app.Configuration.GetValue<ulong?>("Discord:TestGuildId");
        app.AddSlashCommand("ping", "Ping!", () => "Pong!", guildId: guildId);
        app.AddSlashCommand("conch", "Ask the magic conch a question.", MagicConch.Command, guildId: guildId);
        app.AddSlashCommand("sock", "Ask the sock salesman a question.", Sock.Command, guildId: guildId);
        app.UseGatewayEventHandlers();
    }
}
