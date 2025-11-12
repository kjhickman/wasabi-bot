using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace WasabiBot.Api.Components.Pages;

public partial class Home : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private bool IsAuthenticated { get; set; }
    private string? Username { get; set; }
    private string? GlobalName { get; set; }
    private string? DisplayName { get; set; }

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
            }
        }
    }
}

