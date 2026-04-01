using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Credentials;

public static class ListCredentials
{
    public static async Task<IResult> Handle(ClaimsPrincipal user, IApiCredentialService credentialService, CancellationToken cancellationToken)
    {
        if (!CredentialEndpointUser.TryGetOwnerDiscordUserId(user, out var ownerDiscordUserId))
        {
            return Results.Unauthorized();
        }

        var credentials = await credentialService.ListAsync(ownerDiscordUserId, cancellationToken);
        var response = credentials.Select(CredentialResponse.FromSummary);
        return Results.Ok(response);
    }
}
