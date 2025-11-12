using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Components.Pages;

public partial class Home : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private ApiTokenFactory TokenFactory { get; set; } = null!;
    
    [Inject]
    private ILogger<Home> Logger { get; set; } = null!;

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
        Logger.LogInformation("Home.OnInitializedAsync called");
        Logger.LogInformation("ShouldGenerateToken: {ShouldGenerateToken}", ShouldGenerateToken);
        
        if (AuthenticationStateTask is not null)
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            IsAuthenticated = user.Identity?.IsAuthenticated ?? false;
            Logger.LogInformation("IsAuthenticated: {IsAuthenticated}", IsAuthenticated);

            if (IsAuthenticated)
            {
                Username = user.FindFirst("urn:discord:user:username")?.Value
                    ?? user.FindFirst(ClaimTypes.Name)?.Value;
                GlobalName = user.FindFirst("urn:discord:user:global_name")?.Value
                    ?? user.FindFirst("urn:discord:user:globalname")?.Value;

                DisplayName = GlobalName ?? Username ?? "User";
                Logger.LogInformation("DisplayName: {DisplayName}", DisplayName);
                
                // Generate token if form was submitted
                if (ShouldGenerateToken)
                {
                    Logger.LogInformation("Form was submitted, generating token");
                    
                    if (TokenFactory.TryCreateToken(user, out var token))
                    {
                        Logger.LogInformation("Token created successfully, length: {Length}", token.Length);
                        GeneratedToken = token;
                        TokenExpiresAt = DateTimeOffset.UtcNow.Add(TokenFactory.Lifetime);
                        ErrorMessage = null;
                    }
                    else
                    {
                        Logger.LogWarning("Failed to create token");
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
        Logger.LogInformation("HandleSubmit called!");
        
        if (AuthenticationStateTask is not null)
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            Logger.LogInformation("User authenticated, attempting to create token");
            
            if (TokenFactory.TryCreateToken(user, out var token))
            {
                Logger.LogInformation("Token created successfully, length: {Length}", token.Length);
                GeneratedToken = token;
                TokenExpiresAt = DateTimeOffset.UtcNow.Add(TokenFactory.Lifetime);
                ErrorMessage = null;
            }
            else
            {
                Logger.LogWarning("Failed to create token");
                ErrorMessage = "Unable to generate token. Missing user information.";
                GeneratedToken = null;
                TokenExpiresAt = null;
            }
        }
        else
        {
            Logger.LogWarning("AuthenticationStateTask is null");
        }
    }
}

