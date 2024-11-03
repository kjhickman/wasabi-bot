using WasabiBot.Core.Interfaces;
using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddKeyedScoped<IDiscordCommand, PingCommand>(PingCommand.Name);
        services.AddKeyedScoped<IDiscordCommand, DeferredPingCommand>(DeferredPingCommand.Name);
        return services;
    }
}
