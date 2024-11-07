using Microsoft.Extensions.DependencyInjection;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Commands.Handlers;

namespace WasabiBot.DataAccess.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddKeyedScoped<ICommand, PingCommand>(PingCommand.Name);
        services.AddKeyedScoped<ICommand, MagicConchCommand>(MagicConchCommand.Name);
        services.AddKeyedScoped<ICommand, WheelCommand>(WheelCommand.Name);
        return services;
    }
}
