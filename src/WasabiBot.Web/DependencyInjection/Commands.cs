using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Commands;

namespace WasabiBot.Web.DependencyInjection;

public static class Commands
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddKeyedScoped<ICommand, PingCommand>(PingCommand.Name);
        services.AddKeyedScoped<ICommand, MagicConchCommand>(MagicConchCommand.Name);
        services.AddKeyedScoped<ICommand, WheelCommand>(WheelCommand.Name);
        return services;
    }
}