using System.Text;
using Microsoft.Extensions.Options;
using WasabiBot.Extensions;
using WasabiBot.Discord;
using WasabiBot.Settings;

namespace WasabiBot.Filters;

public class DiscordValidationFilter : IEndpointFilter
{
    private readonly EnvironmentVariables _env;
    private readonly ILogger<DiscordValidationFilter> _logger;

    public DiscordValidationFilter(IOptions<EnvironmentVariables> options, ILogger<DiscordValidationFilter> logger)
    {
        _logger = logger;
        _env = options.Value;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
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

        var validSignature = Signature.Verify(_env.DISCORD_PUBLIC_KEY, signature, timestamp, requestBody);
        if (!validSignature)
        {
            _logger.LogInformation("Discord interaction validation failed");
            return TypedResults.BadRequest();
        }
        
        _logger.LogInformation("Discord interaction validated");
        return await next(context);
    }
}
