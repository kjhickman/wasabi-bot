using System.Text;
using Microsoft.Extensions.Options;
using VinceBot.Discord;
using VinceBot.Extensions;
using VinceBot.Settings;

namespace VinceBot.Filters;

public class DiscordValidationFilter : IEndpointFilter
{
    private readonly DiscordSettings _settings;

    public DiscordValidationFilter(IOptions<DiscordSettings> settings)
    {
        _settings = settings.Value;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
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
            // todo: log and add these as parameters
            Console.WriteLine("One or more headers are missing.");
            return TypedResults.BadRequest();
        }

        var validSignature = Signature.Verify(_settings.PublicKey, signature, timestamp, requestBody);
        if (!validSignature)
        {
            Console.WriteLine("Signature verification failed.");
            return TypedResults.BadRequest();
        }

        Console.WriteLine("Signature verification succeeded.");
        return await next(context);
    }
}
