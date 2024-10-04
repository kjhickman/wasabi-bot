using VinceBot.Interfaces;

namespace VinceBot.CommandHandlers;

public static class DependencyInjection
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddKeyedScoped<ICommandHandler, PingHandler>(PingHandler.Name);
        services.AddKeyedScoped<ICommandHandler, DeferredPingHandler>(DeferredPingHandler.Name);
        return services;
    }
}
