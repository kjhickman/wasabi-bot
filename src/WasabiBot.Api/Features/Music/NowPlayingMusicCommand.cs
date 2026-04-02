using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("nowplaying", "Show the currently playing track.")]
internal sealed class NowPlayingMusicCommand(ILogger<NowPlayingMusicCommand> logger, IMusicService musicService)
{
    private readonly ILogger<NowPlayingMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            var result = await _musicService.NowPlayingAsync(ctx);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music command nowplaying failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
