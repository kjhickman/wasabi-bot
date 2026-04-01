using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Credentials;

public static class DeleteCredential
{
    public static async Task<IResult> Handle(
        ClaimsPrincipal user,
        long id,
        IApiCredentialService credentialService,
        CancellationToken cancellationToken)
    {
        if (!CredentialEndpointUser.TryGetOwnerDiscordUserId(user, out var ownerDiscordUserId))
        {
            return Results.Unauthorized();
        }

        var revoked = await credentialService.RevokeAsync(ownerDiscordUserId, id, cancellationToken);
        return revoked ? Results.NoContent() : Results.NotFound();
    }
}
