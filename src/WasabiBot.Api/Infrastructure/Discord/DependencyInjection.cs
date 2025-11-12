using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.Features.CaptionThis.Abstractions;
using WasabiBot.Api.Features.CaptionThis.Services;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Features.Stats;
using WasabiBot.Api.Infrastructure.Discord.EventHandlers;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.Api.Infrastructure.Discord;

internal static class DependencyInjection
{
    public static void AddDiscordServices(this IServiceCollection services)
    {
        services.AddDiscordGateway();
        services.AddApplicationCommands();
        services.AddGatewayHandler<InteractionCreatedEventHandler>();

        // Scans and registers all [CommandHandler] classes
        services.AddDiscordCommandHandlers();

        // Services used for commands
        services.AddScoped<IInteractionService, InteractionService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddSingleton<IReminderTimeCalculator, ReminderTimeCalculator>();
        services.AddSingleton<IReminderStore, InMemoryReminderStore>();
        services.AddHostedService<ReminderProcessor>();
        services.AddScoped<IImageRetrievalService, HttpClientImageRetrievalService>();
        services.AddScoped<IStatsService, StatsService>();
    }
}
