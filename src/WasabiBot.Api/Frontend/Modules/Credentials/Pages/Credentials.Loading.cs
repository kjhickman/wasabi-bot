using WasabiBot.Api.Core.Extensions;

namespace WasabiBot.Api.Frontend.Modules.Credentials;

public partial class Credentials
{
    protected override async Task OnParametersSetAsync()
    {
        ResetPageState();

        var authState = await AuthenticationStateTask;
        var user = authState.User;

        IsAuthenticated = user.Identity?.IsAuthenticated ?? false;
        if (!IsAuthenticated)
        {
            return;
        }

        if (!long.TryParse(user.DiscordUserId, out var ownerDiscordUserId))
        {
            IsAuthenticated = false;
            return;
        }

        OwnerDiscordUserId = ownerDiscordUserId;
        await LoadCredentialsAsync();
    }

    private void ResetPageState()
    {
        CreateCredentialForm ??= new();
        ConfirmCredentialForm ??= new();

        IssuedCredential = null;
        CreateCredentialError = null;
        ConfirmError = null;
        UrlReplacementPath = null;
    }

    private async Task LoadCredentialsAsync()
    {
        if (OwnerDiscordUserId is null)
        {
            return;
        }

        try
        {
            CredentialRows = await ApiCredentialService.ListAsync(OwnerDiscordUserId.Value);
            PageError = null;
        }
        catch
        {
            CredentialRows = [];
            PageError = "Could not load credentials right now.";
        }
    }
}
