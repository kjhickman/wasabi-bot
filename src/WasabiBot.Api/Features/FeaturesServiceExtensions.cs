using NetCord.Hosting.Gateway;
using WasabiBot.Api.Features.CaptionThis.Abstractions;
using WasabiBot.Api.Features.CaptionThis.Services;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Features.Stats;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Discord.EventHandlers;

namespace WasabiBot.Api.Features;

public static class FeaturesServiceExtensions
{
    public static void AddFeatures(this IHostApplicationBuilder builder)
    {
        builder.Services.AddGatewayHandler<InteractionCreatedEventHandler>();

        // Scans and registers all [CommandHandler] classes
        builder.Services.AddDiscordCommandHandlers();

        builder.Services.AddSingleton<IMagicConchTool, MagicConchTool>();
        builder.Services.AddScoped<IInteractionService, InteractionService>();
        builder.Services.AddScoped<ITimeParsingService, TimeParsingService>();
        builder.Services.AddSingleton<IReminderWakeSignal, PostgresReminderWakeSignal>();
        builder.Services.AddHostedService(sp => (PostgresReminderWakeSignal)sp.GetRequiredService<IReminderWakeSignal>());
        builder.Services.AddScoped<IReminderChangeNotifier, PostgresReminderChangeNotifier>();
        builder.Services.AddScoped<IReminderService, ReminderService>();
        builder.Services.AddHostedService<ReminderDispatcher>();
        builder.Services.AddScoped<IImageRetrievalService, HttpClientImageRetrievalService>();
        builder.Services.AddSingleton<RadioTrackMetadataStore>();
        builder.Services.AddSingleton<IMusicPlaybackStatsRecorder, MusicPlaybackStatsRecorder>();
        builder.Services.AddSingleton<IMusicQueueMutationCoordinator, MusicQueueMutationCoordinator>();
        builder.Services.AddSingleton<IMusicInactivityTracker, MusicInactivityTracker>();
        builder.Services.AddScoped<PlaybackService>();
        builder.Services.AddScoped<IMusicService, MusicService>();
        builder.Services.AddScoped<ISharedVoiceChannelResolver, SharedVoiceChannelResolver>();
        builder.Services.AddScoped<IMusicDashboardService, MusicDashboardService>();
        builder.Services.AddScoped<IMusicDashboardControlService, MusicDashboardControlService>();
        builder.Services.AddScoped<IMusicDashboardSearchService, MusicDashboardSearchService>();
        builder.Services.AddScoped<IMusicDashboardQueueService, MusicDashboardQueueService>();
        builder.Services.AddScoped<IMusicFavoritesService, MusicFavoritesService>();
        builder.Services.AddScoped<IMusicGuildStatsService, MusicGuildStatsService>();
        builder.Services.AddHttpClient<IRadioService, RadioService>(client =>
        {
            client.BaseAddress = new Uri("https://de1.api.radio-browser.info/json/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WasabiBot/1.0");
        });
        builder.Services.AddScoped<IStatsService, StatsService>();
    }
}