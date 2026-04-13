using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("leave", "Disconnect from the voice channel.")]
internal sealed class LeaveMusicCommand(ILogger<LeaveMusicCommand> logger, IMusicService musicService, Tracer tracer)
{
    private readonly ILogger<LeaveMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;
    private readonly Tracer _tracer = tracer;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        using var span = _tracer.StartActiveSpan("music.command.leave");
        try
        {
            var result = await _musicService.LeaveAsync(ctx);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Music command leave failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
