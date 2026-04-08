using WasabiBot.Api.Features.ApiCredentials;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Features.OAuth;

public static class GetOAuthToken
{
    public static async Task<IResult> Handle(HttpContext httpContext, IApiCredentialService credentialService, Infrastructure.Auth.ApiTokenFactory tokenFactory)
    {
        var tracer = httpContext.RequestServices.GetRequiredService<Tracer>();
        using var span = tracer.StartActiveSpan("auth.oauth.token.issue");

        ApplyNoStore(httpContext);

        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var grantType = form["grant_type"].ToString();
        if (!string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "unsupported_grant_type" });
        }

        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Results.BadRequest(new { error = "invalid_client" });
        }

        var credential = await credentialService.ValidateAsync(clientId, clientSecret, httpContext.RequestAborted);
        if (credential is null)
        {
            span.SetAttribute("auth.valid_client", false);
            return Results.BadRequest(new { error = "invalid_client" });
        }

        span.SetAttribute("auth.valid_client", true);

        var response = new TokenResponse
        {
            AccessToken = tokenFactory.CreateToken(credential),
            ExpiresIn = (int)tokenFactory.Lifetime.TotalSeconds,
        };

        return Results.Ok(response);
    }

    private static void ApplyNoStore(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        httpContext.Response.Headers.Pragma = "no-cache";
        httpContext.Response.Headers.Expires = "0";
    }
}
