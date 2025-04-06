using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;

namespace WasabiBot.Api.Modules;

public static class DependencyInjection
{
    public static void AddDiscord(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
    }

    public static void MapDiscordCommands(this WebApplication app)
    {
        var guildId = app.Configuration.GetValue<ulong?>("Discord:TestGuildId");
        app.AddSlashCommand("ping", "Ping!", () => "Pong!", guildId: guildId);
        app.AddSlashCommand("conch", "Ask the magic conch a question.", MagicConch.Command, guildId: guildId);
        app.UseGatewayEventHandlers();
    }
}
