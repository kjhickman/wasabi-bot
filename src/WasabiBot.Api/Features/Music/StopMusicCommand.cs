using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("stop", "Stop playback and clear the queue.")]
internal sealed class StopMusicCommand(ILogger<StopMusicCommand> logger, IMusicService musicService)
{
    private readonly ILogger<StopMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            var result = await _musicService.StopAsync(ctx);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music command stop failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
