using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace WasabiBot.Api.Infrastructure.Auth;

internal sealed class ApiTokenFactory
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    private readonly ApiTokenOptions _options;

    public ApiTokenFactory(ApiTokenOptions options)
    {
        _options = options;
    }

    public string TokenType => "Bearer";

    public int LifetimeSeconds => (int)_options.Lifetime.TotalSeconds;

    public bool TryCreateToken(ClaimsPrincipal user, out string token)
    {
        token = string.Empty;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var usernameClaim = user.FindFirst(ClaimTypes.Name) ?? user.FindFirst("urn:discord:user:username");
        if (usernameClaim is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, usernameClaim.Value));
        }

        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.Add(_options.Lifetime),
            signingCredentials: new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256)
        );

        token = TokenHandler.WriteToken(jwt);
        return true;
    }
}
