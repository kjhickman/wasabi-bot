using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Music;

[CommandHandler("nowplaying", "Show the currently playing track or radio station.")]
internal sealed class NowPlayingMusicCommand(ILogger<NowPlayingMusicCommand> logger, IMusicService musicService, Tracer tracer)
{
    private readonly ILogger<NowPlayingMusicCommand> _logger = logger;
    private readonly IMusicService _musicService = musicService;
    private readonly Tracer _tracer = tracer;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        using var span = _tracer.StartActiveSpan("music.command.now-playing");
        span.SetAttribute("discord.user_id", ctx.UserId.ToString());
        span.SetAttribute("discord.channel_id", ctx.ChannelId.ToString());
        try
        {
            var result = await _musicService.NowPlayingAsync(ctx);
            span.SetAttribute("music.response.ephemeral", result.Ephemeral);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Music command nowplaying failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that music command.");
        }
    }
}
