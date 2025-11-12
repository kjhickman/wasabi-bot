using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Core.Extensions;

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
            DisplayName = user.DisplayName;
        }
    }
}

