using System.Security.Claims;

namespace WasabiBot.Api.Infrastructure.Auth.Endpoints;

public static class RefreshToken
{
    internal static IResult Handle(ClaimsPrincipal user, ApiTokenFactory tokenFactory)
    {
        if (!tokenFactory.TryCreateToken(user, out var token))
        {
            return Results.BadRequest(new { error = "missing_user_id" });
        }

        return Results.Ok(new
        {
            access_token = token,
            token_type = tokenFactory.TokenType,
            expires_in = tokenFactory.LifetimeSeconds
        });
    }
}
