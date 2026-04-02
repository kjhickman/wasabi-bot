using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Core.Extensions;

namespace WasabiBot.Api.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    public required IAuthorizationService AuthorizationService { get; set; }

    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationStateTask { get; set; }

    private bool IsAuthenticated { get; set; }
    private bool HasGuildAccess { get; set; }
    private string? DisplayName { get; set; }
    private string PageTitleText => !IsAuthenticated ? "Sign In" : HasGuildAccess ? $"Dashboard - {DisplayName}" : "Access Restricted";

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated)
        {
            return;
        }

        DisplayName = user.DisplayName;
        HasGuildAccess = (await AuthorizationService.AuthorizeAsync(user, policyName: "DiscordGuildMember")).Succeeded;
    }
}
