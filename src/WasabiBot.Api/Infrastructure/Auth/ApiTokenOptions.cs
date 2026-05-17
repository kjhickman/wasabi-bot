using Microsoft.IdentityModel.Tokens;

namespace WasabiBot.Api.Infrastructure.Auth;

public sealed class ApiTokenOptions(SymmetricSecurityKey signingKey, TimeSpan lifetime)
{
    public SymmetricSecurityKey SigningKey { get; } = signingKey;

    public TimeSpan Lifetime { get; } = lifetime;
}
