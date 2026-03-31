using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.UnitTests.Components;

internal static class ComponentTestHelpers
{
    public static ApiTokenFactory CreateTokenFactory(TimeSpan? lifetime = null)
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var signingKey = new SymmetricSecurityKey(key);
        var options = new ApiTokenOptions(signingKey, lifetime ?? TimeSpan.FromHours(1));
        return new ApiTokenFactory(options);
    }
}
