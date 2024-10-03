namespace VinceBot.Extensions;

public static class HttpRequestExtensions
{
    public static string? GetHeaderValue(this HttpRequest request, string headerName)
    {
        if (request.Headers.TryGetValue(headerName, out var value))
        {
            return value;
        }

        return null;
    }
}