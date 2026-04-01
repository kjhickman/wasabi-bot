using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Credentials;

public static class CreateCredential
{
    public static async Task<IResult> Handle(
        ClaimsPrincipal user,
        CreateCredentialRequest request,
        IApiCredentialService credentialService,
        CancellationToken cancellationToken)
    {
        if (!CredentialEndpointUser.TryGetOwnerDiscordUserId(user, out var ownerDiscordUserId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = await credentialService.CreateAsync(ownerDiscordUserId, request.Name, cancellationToken);
            return Results.Ok(CredentialIssueResponse.FromResult(result));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
