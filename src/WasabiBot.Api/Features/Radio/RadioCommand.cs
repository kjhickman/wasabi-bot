using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Radio;

[CommandHandler("radio", "Search internet radio stations and play the best match.")]
internal sealed class RadioCommand(ILogger<RadioCommand> logger, IRadioService radioService, Tracer tracer)
{
    private readonly ILogger<RadioCommand> _logger = logger;
    private readonly IRadioService _radioService = radioService;
    private readonly Tracer _tracer = tracer;

    public async Task ExecuteAsync(
        ICommandContext ctx,
        [SlashCommandParameter(Description = "Internet radio station search query")] string query)
    {
        using var span = _tracer.StartActiveSpan("radio.command.play");

        try
        {
            var result = await _radioService.PlayAsync(ctx, query);
            await ctx.RespondAsync(result.Message, result.Ephemeral);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Radio command failed for user {UserId}", ctx.UserId);
            await ctx.SendEphemeralAsync("Something went wrong while processing that radio command.");
        }
    }
}
