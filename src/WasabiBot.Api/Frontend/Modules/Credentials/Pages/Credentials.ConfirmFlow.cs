namespace WasabiBot.Api.Frontend.Modules.Credentials;

public partial class Credentials
{
    private async Task HandleConfirmSubmit()
    {
        await ConfirmPendingActionAsync();
    }

    private async Task ConfirmPendingActionAsync()
    {
        if (OwnerDiscordUserId is null || CurrentConfirmAction is null)
        {
            return;
        }

        var credential = ConfirmCredential;
        if (credential is null)
        {
            ConfirmError = "Credential not found.";
            return;
        }

        try
        {
            switch (CurrentConfirmAction)
            {
                case PendingCredentialAction.Delete:
                    await ApiCredentialService.RevokeAsync(OwnerDiscordUserId.Value, credential.Id);
                    await LoadCredentialsAsync();
                    UrlReplacementPath = "/creds";
                    break;
                case PendingCredentialAction.Regenerate:
                    var issuedCredential = await ApiCredentialService.RegenerateSecretAsync(OwnerDiscordUserId.Value, credential.Id);
                    if (issuedCredential is null)
                    {
                        ConfirmError = "Could not regenerate this credential right now.";
                        return;
                    }

                    await LoadCredentialsAsync();
                    IssuedCredential = issuedCredential;
                    UrlReplacementPath = "/creds";
                    break;
            }
        }
        catch
        {
            ConfirmError = CurrentConfirmAction == PendingCredentialAction.Delete
                ? "Could not delete this credential right now."
                : "Could not regenerate this credential right now.";
        }
    }
}
