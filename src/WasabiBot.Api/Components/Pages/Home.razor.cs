using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Components.Pages;

public partial class Home : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private ApiTokenFactory TokenFactory { get; set; } = null!;

    [SupplyParameterFromForm]
    private bool ShouldGenerateToken { get; set; }

    private bool IsAuthenticated { get; set; }
    private string? Username { get; set; }
    private string? GlobalName { get; set; }
    private string? DisplayName { get; set; }
    private string? GeneratedToken { get; set; }
    private DateTimeOffset? TokenExpiresAt { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is not null)
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

            if (IsAuthenticated)
            {
                Username = user.FindFirst("urn:discord:user:username")?.Value
                    ?? user.FindFirst(ClaimTypes.Name)?.Value;
                GlobalName = user.FindFirst("urn:discord:user:global_name")?.Value
                    ?? user.FindFirst("urn:discord:user:globalname")?.Value;

                DisplayName = GlobalName ?? Username ?? "User";
                
                // Generate token if form was submitted
                if (ShouldGenerateToken)
                {
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
    }

    private async Task HandleSubmit()
    {
        if (AuthenticationStateTask is not null)
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

