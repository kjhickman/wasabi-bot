using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.Api.Features.RemindMe;
using WasabiBot.Api.Features.RemindMe.Abstractions;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.Api.Infrastructure.Discord.EventHandlers;
using WasabiBot.Api.Features.Spin;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Services;

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
        services.AddHostedService<ReminderProcessor>();
        services.AddScoped<IInteractionService, InteractionService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<CaptionThisCommand>();
        services.AddScoped<RemindMeCommand>();
        services.AddScoped<SpinCommand>();
        services.AddScoped<MagicConchCommand>();
    }

    public static void MapDiscordCommands(this IHost app)
    {
        app.MapCommandHandler<MagicConchCommand>();
        app.MapCommandHandler<CaptionThisCommand>();
        app.MapCommandHandler<RemindMeCommand>();
        app.MapCommandHandler<SpinCommand>();
    }

    private static void MapCommandHandler<T>(this IHost app) where T : CommandBase
    {
        var scope = app.Services.CreateScope();
        var command = scope.ServiceProvider.GetRequiredService<T>();
        app.AddSlashCommand(command.Command, command.Description, command.Handler);
    }
}
