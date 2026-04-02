using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("queue", "Show the current queue.")]
internal sealed class QueueMusicCommand(ILogger<QueueMusicCommand> logger, IMusicService musicService)
{
    private readonly ILogger<QueueMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            var result = await _musicService.QueueAsync(ctx);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music command queue failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
