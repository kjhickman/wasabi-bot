using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Credentials;

public static class RegenerateCredentialSecret
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

        var result = await credentialService.RegenerateSecretAsync(ownerDiscordUserId, id, cancellationToken);
        return result is null
            ? Results.NotFound()
            : Results.Ok(CredentialIssueResponse.FromResult(result));
    }
}
