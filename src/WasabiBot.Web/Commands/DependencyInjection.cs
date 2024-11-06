using WasabiBot.Core.Interfaces;
using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddKeyedScoped<ICommand, PingCommand>(PingCommand.Name);
        services.AddKeyedScoped<ICommand, MagicConchCommand>(MagicConchCommand.Name);
        return services;
    }
}
