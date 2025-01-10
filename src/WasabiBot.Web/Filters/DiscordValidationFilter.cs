using System.Text;
using Discord.Rest;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using WasabiBot.Discord;

namespace WasabiBot.Web.Filters;

public class DiscordValidationFilter : IEndpointFilter
{
    private readonly DiscordSettings _settings;
    private readonly DiscordRestClient _discord;
    private readonly ILogger<DiscordValidationFilter> _logger;
    private readonly Tracer _tracer;

    public DiscordValidationFilter(IOptions<DiscordSettings> options, ILogger<DiscordValidationFilter> logger,
        Tracer tracer, DiscordRestClient discord)
    {
        _logger = logger;
        _tracer = tracer;
        _discord = discord;
        _settings = options.Value;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(DiscordValidationFilter)}.{nameof(InvokeAsync)}");
        _logger.LogInformation("Validating discord interaction");
        var request = context.HttpContext.Request;

        // Ensure the body can be read multiple times
        request.EnableBuffering();

        var signature = request.Headers["x-signature-ed25519"];
        var timestamp = request.Headers["x-signature-timestamp"];
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var requestBody = await reader.ReadToEndAsync();

        // Reset the stream position so the next middleware can read it
        request.Body.Position = 0;
        
        RestInteraction? interaction;
        try
        {
            interaction = await _discord.ParseHttpInteractionAsync(_settings.PublicKey, signature, timestamp, requestBody, _ => false);
        }
        catch (BadSignatureException)
        {
            _logger.LogWarning("Received interaction with invalid signature");
            return Results.BadRequest();
        }

        context.HttpContext.Items["Interaction"] = interaction;
        
        span.End();
        return await next(context);
    }
}