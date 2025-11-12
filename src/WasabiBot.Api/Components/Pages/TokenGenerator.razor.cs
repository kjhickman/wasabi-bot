using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Components.Pages;

public partial class TokenGenerator : ComponentBase
{
    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationStateTask { get; set; }

    [Inject]
    public required ApiTokenFactory TokenFactory { get; set; }

    [SupplyParameterFromForm]
    private bool ShouldGenerateToken { get; set; }

    private string? GeneratedToken { get; set; }
    private DateTimeOffset? TokenExpiresAt { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Generate token if form was submitted - results in entire page re-render. Temporary solution
        if (ShouldGenerateToken)
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (TokenFactory.TryCreateToken(user, out var token))
            {
                GeneratedToken = token;
                TokenExpiresAt = DateTimeOffset.UtcNow.Add(TokenFactory.Lifetime);
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
}
