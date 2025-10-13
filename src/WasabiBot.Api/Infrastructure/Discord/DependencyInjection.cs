using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.Api.Infrastructure.Discord.EventHandlers;

namespace WasabiBot.Api.Infrastructure.Discord;

internal static class DependencyInjection
{
    public static void AddDiscordInfrastructure(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
        services.AddGatewayHandler<InteractionCreatedEventHandler>();

        // Services used for commands
        // services.AddSingleton<ReminderTimeCalculator>();
    }

    public static void MapDiscordCommands(this IHost app)
    {
        app.AddSlashCommand(MagicConchCommand.Name, MagicConchCommand.Description, MagicConchCommand.ExecuteAsync);
        app.AddSlashCommand(CaptionThisCommand.Name, CaptionThisCommand.Description, CaptionThisCommand.ExecuteAsync);
        // app.AddSlashCommand(RemindMeCommand.Name, RemindMeCommand.Description, RemindMeCommand.ExecuteAsync);
    }
}
