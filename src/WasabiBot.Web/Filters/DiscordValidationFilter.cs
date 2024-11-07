using System.Text;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Extensions;
using WasabiBot.Web.Settings;

namespace WasabiBot.Web.Filters;

public class DiscordValidationFilter : IEndpointFilter
{
    private readonly DiscordSettings _discordSettings;
    private readonly ILogger<DiscordValidationFilter> _logger;
    private readonly Tracer _tracer;

    public DiscordValidationFilter(IOptions<DiscordSettings> options, ILogger<DiscordValidationFilter> logger,
        Tracer tracer)
    {
        _logger = logger;
        _tracer = tracer;
        _discordSettings = options.Value;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(DiscordValidationFilter)}.{nameof(InvokeAsync)}");
        _logger.LogInformation("Validating discord interaction");
        var request = context.HttpContext.Request;

        // Ensure the body can be read multiple times
        request.EnableBuffering();

        var signature = request.GetHeaderValue("x-signature-ed25519");
        var timestamp = request.GetHeaderValue("x-signature-timestamp");
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var requestBody = await reader.ReadToEndAsync();

        // Reset the stream position so the next middleware can read it
        request.Body.Position = 0;

        if (signature is null || timestamp is null)
        {
            _logger.LogError("Missing {signature} or {timestamp}", signature, timestamp);
            return TypedResults.BadRequest();
        }

        var validSignature = Signature.Verify(_discordSettings.PublicKey, signature, timestamp, requestBody);
        if (!validSignature)
        {
            _logger.LogInformation("Discord interaction validation failed");
            return TypedResults.BadRequest();
        }
        
        span.End();
        return await next(context);
    }
}
