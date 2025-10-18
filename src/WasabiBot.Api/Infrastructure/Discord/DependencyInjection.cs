using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.Api.Features.RemindMe;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Features.RemindMe.Services;
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
        services.AddSingleton<IReminderTimeCalculator, ReminderTimeCalculator>();
        services.AddSingleton<IReminderStore, InMemoryReminderStore>();
        services.AddTransient<IReminderService, ReminderService>();
        services.AddHostedService<ReminderProcessor>();
    }

    public static void MapDiscordCommands(this IHost app)
    {
        app.AddSlashCommand(MagicConchCommand.Name, MagicConchCommand.Description, MagicConchCommand.ExecuteAsync);
        app.AddSlashCommand(CaptionThisCommand.Name, CaptionThisCommand.Description, CaptionThisCommand.ExecuteAsync);
        app.AddSlashCommand(RemindMeCommand.Name, RemindMeCommand.Description, RemindMeCommand.ExecuteAsync);
    }
}
