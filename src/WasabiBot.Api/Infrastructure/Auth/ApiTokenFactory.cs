using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Features.ApiCredentials;

namespace WasabiBot.Api.Infrastructure.Auth;

public sealed class ApiTokenFactory
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    private readonly ApiTokenOptions _options;

    public ApiTokenFactory(ApiTokenOptions options)
    {
        _options = options;
    }

    public TimeSpan Lifetime => _options.Lifetime;

    public string CreateToken(ApiCredentialValidationResult credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, credential.OwnerDiscordUserId.ToString()),
            new(JwtRegisteredClaimNames.Sub, credential.OwnerDiscordUserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", credential.ClientId),
            new("credential_id", credential.CredentialId.ToString()),
            new("grant_type", "client_credentials")
        ];

        return WriteToken(claims);
    }

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

        token = WriteToken(claims);
        return true;
    }

    private string WriteToken(IEnumerable<Claim> claims)
    {
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.Add(_options.Lifetime),
            signingCredentials: new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256)
        );

        return TokenHandler.WriteToken(jwt);
    }
}
