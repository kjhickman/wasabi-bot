using System.Security.Claims;

namespace WasabiBot.Api.Features.Credentials;

internal static class CredentialEndpointUser
{
    public static bool TryGetOwnerDiscordUserId(ClaimsPrincipal user, out long ownerDiscordUserId)
    {
        ownerDiscordUserId = default;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim)
               && long.TryParse(userIdClaim, out ownerDiscordUserId);
    }
}
