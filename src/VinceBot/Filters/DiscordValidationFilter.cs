using System.Text;
using VinceBot.Discord;
using VinceBot.Extensions;

namespace VinceBot.Filters;

public class DiscordValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        
        // Ensure the body can be read multiple times
        request.EnableBuffering();
        
        var publicKey = Environment.GetEnvironmentVariable("DISCORD_PUBLIC_KEY");
        var signature = request.GetHeaderValue("x-signature-ed25519");
        var timestamp = request.GetHeaderValue("x-signature-timestamp");
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var requestBody = await reader.ReadToEndAsync();
        
        // Reset the stream position so the next handler can read it
        request.Body.Position = 0;
    
        Console.WriteLine($"Received request. sig: {signature}, ts: {timestamp}, body: {requestBody}");

        if (publicKey is null || signature is null || timestamp is null)
        {
            Console.WriteLine("One or more parameters are missing.");
            return TypedResults.BadRequest();
        }
        
        var valid = Signature.Verify(publicKey, signature, timestamp, requestBody);
        if (!valid)
        {
            Console.WriteLine("Signature verification failed.");
            return TypedResults.BadRequest();
        }

        Console.WriteLine("Signature verification succeeded.");
        return await next(context);
    }
}