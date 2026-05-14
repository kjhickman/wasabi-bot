using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Features.Ask;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.Api.Features.Choose;
using WasabiBot.Api.Features.Flip;
using WasabiBot.Api.Features.Help;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.Api.Features.Mock;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Features.Reminders;
using WasabiBot.Api.Features.Stats;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Infrastructure.Discord;

internal static class DiscordCommandHandlerExtensions
{
    public static IServiceCollection AddDiscordCommandHandlers(this IServiceCollection services)
    {
        services.AddScoped<AskCommand>();
        services.AddScoped<CaptionThisCommand>();
        services.AddScoped<ChooseCommand>();
        services.AddScoped<FlipCommand>();
        services.AddScoped<HelpCommand>();
        services.AddScoped<MagicConchCommand>();
        services.AddScoped<MockCommand>();
        services.AddScoped<LeaveMusicCommand>();
        services.AddScoped<NowPlayingMusicCommand>();
        services.AddScoped<PlayMusicCommand>();
        services.AddScoped<QueueMusicCommand>();
        services.AddScoped<SkipMusicCommand>();
        services.AddScoped<StopMusicCommand>();
        services.AddScoped<RadioCommand>();
        services.AddScoped<RemindMeCommand>();
        services.AddScoped<RemindMeListCommand>();
        services.AddScoped<RemindMeRmCommand>();
        services.AddScoped<StatsCommand>();

        return services;
    }

    public static IHost MapDiscordCommandHandlers(this IHost app)
    {
        app.AddSlashCommand("ask", "Ask a quick one-off question.", (AskCommand handler, ApplicationCommandContext ctx, [SlashCommandParameter(Description = "Your question")] string question) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, question);
        });

        app.AddSlashCommand("caption", "Generate a funny caption for an image.", (CaptionThisCommand handler, ApplicationCommandContext ctx, global::NetCord.Attachment image) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, image);
        });

        app.AddSlashCommand("choose", "Choose randomly from 2-7 options.", (ChooseCommand handler, ApplicationCommandContext ctx, string option1, string option2, string? option3 = null, string? option4 = null, string? option5 = null, string? option6 = null, string? option7 = null) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, option1, option2, option3, option4, option5, option6, option7);
        });

        app.AddSlashCommand("flip", "Flip a coin.", (FlipCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("help", "Shows all available commands and helpful links.", (HelpCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("conch", "Ask the magic conch a question.", (MagicConchCommand handler, ApplicationCommandContext ctx, [SlashCommandParameter(Description = "A yes/no question")] string question) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, question);
        });

        app.AddMessageCommand("Mock", async (global::NetCord.Rest.RestMessage message) =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<MockCommand>();
            return await handler.ExecuteAsync(message);
        });

        app.AddSlashCommand("leave", "Disconnect from the voice channel.", (LeaveMusicCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("nowplaying", "Show the currently playing track or radio station.", (NowPlayingMusicCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("play", "Play music from a SoundCloud search query or URL.", (PlayMusicCommand handler, ApplicationCommandContext ctx, [SlashCommandParameter(Description = "SoundCloud search query or track/playlist URL")] string input) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, input);
        });

        app.AddSlashCommand("queue", "Show the current queue.", (QueueMusicCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("skip", "Skip the current track.", (SkipMusicCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("stop", "Stop playback and clear the queue.", (StopMusicCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("radio", "Search internet radio stations and play the best match.", (RadioCommand handler, ApplicationCommandContext ctx, [SlashCommandParameter(Description = "Internet radio station search query")] string query) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, query);
        });

        app.AddSlashCommand("reminder", "Set a reminder for the channel.", (RemindMeCommand handler, ApplicationCommandContext ctx, [SlashCommandParameter(Description = "When do you want to be reminded?")] string when, [SlashCommandParameter(Description = "Your reminder")] string reminder) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, when, reminder);
        });

        app.AddSlashCommand("reminder-list", "List all your reminders.", (RemindMeListCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        app.AddSlashCommand("reminder-rm", "Delete a reminder.", (RemindMeRmCommand handler, ApplicationCommandContext ctx, int reminderId) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext, reminderId);
        });

        app.AddSlashCommand("stats", "Show bot usage statistics.", (StatsCommand handler, ApplicationCommandContext ctx) =>
        {
            var commandContext = new WasabiCommandContext(ctx);
            return handler.ExecuteAsync(commandContext);
        });

        return app;
    }
}
