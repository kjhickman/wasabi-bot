using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Components.Pages;

public partial class TokenGenerator(ApiTokenFactory tokenFactory) : ComponentBase
{
    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationStateTask { get; set; }

    private string? GeneratedToken { get; set; }
    private DateTimeOffset? TokenExpiresAt { get; set; }
    private string? ErrorMessage { get; set; }

    // Registered as the handler for tokenForm
    protected async Task GenerateToken()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;

        if (tokenFactory.TryCreateToken(user, out var token))
        {
            GeneratedToken = token;
            TokenExpiresAt = DateTimeOffset.UtcNow.Add(tokenFactory.Lifetime);
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = "Unable to generate token. Missing user information.";
            GeneratedToken = null;
            TokenExpiresAt = null;
        }
    }
}
