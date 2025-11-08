using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Token;

public static class GetToken
{
    public static IResult Handle(ClaimsPrincipal user, ApiTokenFactory tokenFactory)
    {
        if (!tokenFactory.TryCreateToken(user, out var token))
        {
            return Results.BadRequest(new { error = "missing_user_id" });
        }

        var response = new TokenResponse
        {
            AccessToken = token,
            TokenType = tokenFactory.TokenType,
            ExpiresIn = tokenFactory.LifetimeSeconds
        };

        return Results.Ok(response);
    }
}
