using WasabiBot.Commands.Handlers;
using WasabiBot.Interfaces;

namespace WasabiBot.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddKeyedScoped<CommandHandler, PingHandler>(PingHandler.Name);
        services.AddKeyedScoped<CommandHandler, DeferredPingHandler>(DeferredPingHandler.Name);
        return services;
    }
}
