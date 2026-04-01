using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Token;

public static class GetToken
{
    public static IResult Handle(HttpContext httpContext, ClaimsPrincipal user, ApiTokenFactory tokenFactory)
    {
        ApplyNoStore(httpContext);
        return Handle(user, tokenFactory);
    }

    public static IResult Handle(ClaimsPrincipal user, ApiTokenFactory tokenFactory)
    {
        if (!tokenFactory.TryCreateToken(user, out var token))
        {
            return Results.BadRequest(new { error = "missing_user_id" });
        }

        var response = new TokenResponse
        {
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.Add(tokenFactory.Lifetime)
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
