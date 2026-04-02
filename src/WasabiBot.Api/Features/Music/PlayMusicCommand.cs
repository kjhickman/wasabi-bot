using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("play", "Play music from a search query or direct URL.")]
internal sealed class PlayMusicCommand(ILogger<PlayMusicCommand> logger, IMusicService musicService)
{
    private readonly ILogger<PlayMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    public async Task ExecuteAsync(
        ICommandContext ctx,
        [SlashCommandParameter(Description = "Search query or direct track/playlist URL")] string input)
    {
        await HandleAsync(ctx, () => _musicService.PlayAsync(ctx, input), nameof(ExecuteAsync));
    }

    private async Task HandleAsync(ICommandContext ctx, Func<Task<MusicCommandResult>> action, string actionName)
    {
        try
        {
            var result = await action();
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music command {Action} failed for user {UserId}", actionName, ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
