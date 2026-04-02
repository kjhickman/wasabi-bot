using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("leave", "Disconnect from the voice channel.")]
internal sealed class LeaveMusicCommand(ILogger<LeaveMusicCommand> logger, IMusicService musicService)
{
    private readonly ILogger<LeaveMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            var result = await _musicService.LeaveAsync(ctx);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music command leave failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
