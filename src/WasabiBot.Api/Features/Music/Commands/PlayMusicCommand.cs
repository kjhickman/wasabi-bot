using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("play", "Play music from a SoundCloud search query or URL.")]
internal sealed class PlayMusicCommand(ILogger<PlayMusicCommand> logger, IMusicService musicService, Tracer tracer)
{
    private readonly ILogger<PlayMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;
    private readonly Tracer _tracer = tracer;

    public async Task ExecuteAsync(
        ICommandContext ctx,
        [SlashCommandParameter(Description = "SoundCloud search query or track/playlist URL")] string input)
    {
        await HandleAsync(ctx, () => _musicService.PlayAsync(ctx, input), nameof(ExecuteAsync));
    }

    private async Task HandleAsync(ICommandContext ctx, Func<Task<MusicCommandResult>> action, string actionName)
    {
        using var span = _tracer.StartActiveSpan("music.command.play");
        try
        {
            var result = await action();
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Music command {Action} failed for user {UserId}", actionName, ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
