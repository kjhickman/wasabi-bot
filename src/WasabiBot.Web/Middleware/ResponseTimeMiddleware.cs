namespace WasabiBot.Web.Middleware;

public class ResponseTimeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseTimeMiddleware> _logger;
    private readonly TimeProvider _timeProvider;

    public ResponseTimeMiddleware(RequestDelegate next, ILogger<ResponseTimeMiddleware> logger, TimeProvider timeProvider)
    {
        _next = next;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = _timeProvider.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = _timeProvider.GetElapsedTime(start);
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Contains("interaction") && elapsed.TotalMilliseconds > 2000)
            {
                _logger.LogWarning("Long response time: {elapsed}ms", elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogInformation("Response time: {elapsed}ms", elapsed.TotalMilliseconds);
            }
        }
    }
}