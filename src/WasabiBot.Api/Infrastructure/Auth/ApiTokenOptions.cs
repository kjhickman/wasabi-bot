using System;
using Microsoft.IdentityModel.Tokens;

namespace WasabiBot.Api.Infrastructure.Auth;

internal sealed class ApiTokenOptions
{
    public ApiTokenOptions(SymmetricSecurityKey signingKey, TimeSpan lifetime)
    {
        SigningKey = signingKey;
        Lifetime = lifetime;
    }

    public SymmetricSecurityKey SigningKey { get; }

    public TimeSpan Lifetime { get; }
}
