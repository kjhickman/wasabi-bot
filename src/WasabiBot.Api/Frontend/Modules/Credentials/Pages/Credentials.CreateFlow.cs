namespace WasabiBot.Api.Frontend.Modules.Credentials;

public partial class Credentials
{
    private async Task HandleCreateSubmit()
    {
        await CreateCredentialAsync();
    }

    private async Task CreateCredentialAsync()
    {
        if (OwnerDiscordUserId is null)
        {
            return;
        }

        CreateCredentialError = ValidateCredentialName(CreateCredentialFormName);
        if (!string.IsNullOrEmpty(CreateCredentialError))
        {
            return;
        }

        try
        {
            var issuedCredential = await ApiCredentialService.CreateAsync(OwnerDiscordUserId.Value, CreateCredentialFormName.Trim());
            await LoadCredentialsAsync();

            IssuedCredential = issuedCredential;
            UrlReplacementPath = "/creds";
            CreateCredentialForm = new();
        }
        catch (ArgumentException ex)
        {
            CreateCredentialError = ex.Message;
        }
        catch
        {
            CreateCredentialError = "Could not create credential right now.";
        }
    }

    private static string? ValidateCredentialName(string name)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return "Credential name is required.";
        }

        if (normalizedName.Length > MaxCredentialNameLength)
        {
            return $"Credential name must be {MaxCredentialNameLength} characters or fewer.";
        }

        return normalizedName.Any(char.IsControl)
            ? "Credential name cannot contain control characters."
            : null;
    }
}
