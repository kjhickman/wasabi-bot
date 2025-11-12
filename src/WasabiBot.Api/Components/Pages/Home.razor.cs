using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace WasabiBot.Api.Components.Pages;

public partial class Home : ComponentBase
{
    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationStateTask { get; set; }

    private bool IsAuthenticated { get; set; }
    private string? DisplayName { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;

        IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

        if (IsAuthenticated)
        {
            var username = user.FindFirst("urn:discord:user:username")?.Value
                ?? user.FindFirst(ClaimTypes.Name)?.Value;
            var globalName = user.FindFirst("urn:discord:user:global_name")?.Value
                ?? user.FindFirst("urn:discord:user:globalname")?.Value;

            DisplayName = globalName ?? username ?? "User";
        }
    }
}

