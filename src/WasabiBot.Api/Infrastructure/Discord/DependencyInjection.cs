using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Features.CaptionThis.Abstractions;
using WasabiBot.Api.Features.CaptionThis.Services;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Features.Stats;
using WasabiBot.Api.Infrastructure.Discord.EventHandlers;

namespace WasabiBot.Api.Infrastructure.Discord;

internal static class DependencyInjection
{
    public static void AddDiscordServices(this IServiceCollection services)
    {
        services.AddDiscordGateway(x =>
        {
            x.Intents = GatewayIntents.AllNonPrivileged;
            x.Presence = new PresenceProperties(UserStatusType.Online)
            {
                Since = DateTimeOffset.UtcNow,
                Activities = [ new UserActivityProperties("custom", UserActivityType.Custom)
                {
                    State = "Type /help for commands"
                }],
                Afk = false
            };
        });

        services.AddApplicationCommands();
        services.AddGatewayHandler<InteractionCreatedEventHandler>();

        // Scans and registers all [CommandHandler] classes
        services.AddDiscordCommandHandlers();

        services.AddScoped<IInteractionService, InteractionService>();
        services.AddScoped<ITimeParsingService, TimeParsingService>();
        services.AddSingleton<IReminderWakeSignal, PostgresReminderWakeSignal>();
        services.AddHostedService(sp => (PostgresReminderWakeSignal)sp.GetRequiredService<IReminderWakeSignal>());
        services.AddScoped<IReminderChangeNotifier, PostgresReminderChangeNotifier>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddHostedService<ReminderDispatcher>();
        services.AddScoped<IImageRetrievalService, HttpClientImageRetrievalService>();
        services.AddSingleton<RadioTrackMetadataStore>();
        services.AddSingleton<IMusicPlaybackStatsRecorder, MusicPlaybackStatsRecorder>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<IMusicService, MusicService>();
        services.AddScoped<ISharedVoiceChannelResolver, SharedVoiceChannelResolver>();
        services.AddScoped<IMusicDashboardService, MusicDashboardService>();
        services.AddScoped<IMusicDashboardControlService, MusicDashboardControlService>();
        services.AddScoped<IMusicDashboardSearchService, MusicDashboardSearchService>();
        services.AddScoped<IMusicDashboardQueueService, MusicDashboardQueueService>();
        services.AddScoped<IMusicFavoritesService, MusicFavoritesService>();
        services.AddScoped<IMusicGuildStatsService, MusicGuildStatsService>();
        services.AddHttpClient<IRadioService, RadioService>(client =>
        {
            client.BaseAddress = new Uri("https://de1.api.radio-browser.info/json/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WasabiBot/1.0");
        });
        services.AddScoped<IStatsService, StatsService>();
    }
}
