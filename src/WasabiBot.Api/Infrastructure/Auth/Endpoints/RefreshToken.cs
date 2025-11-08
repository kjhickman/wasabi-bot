using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace WasabiBot.Api.Infrastructure.Auth.Endpoints;

public static class RefreshToken
{
    internal static IResult Handle(ClaimsPrincipal user, ApiTokenOptions tokenOptions)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null)
        {
            return Results.BadRequest(new { error = "missing_user_id" });
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userIdClaim.Value),
            new(JwtRegisteredClaimNames.Sub, userIdClaim.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var usernameClaim = user.FindFirst(ClaimTypes.Name) ?? user.FindFirst("urn:discord:user:username");
        if (usernameClaim is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, usernameClaim.Value));
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.Add(tokenOptions.Lifetime),
            signingCredentials: new SigningCredentials(tokenOptions.SigningKey, SecurityAlgorithms.HmacSha256)
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Ok(new
        {
            access_token = tokenString,
            token_type = "Bearer",
            expires_in = (int)tokenOptions.Lifetime.TotalSeconds
        });
    }
}
