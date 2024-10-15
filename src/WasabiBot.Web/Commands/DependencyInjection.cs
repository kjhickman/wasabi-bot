using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddKeyedScoped<CommandBase, PingCommand>(PingCommand.Name);
        // services.AddKeyedScoped<CommandBase, DeferredPingCommand>(DeferredPingCommand.Name);
        return services;
    }
}
