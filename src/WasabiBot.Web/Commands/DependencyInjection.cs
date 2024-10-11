using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddKeyedScoped<CommandHandlerBase, PingHandlerBase>(PingHandlerBase.Name);
        services.AddKeyedScoped<CommandHandlerBase, DeferredPingHandlerBase>(DeferredPingHandlerBase.Name);
        return services;
    }
}
