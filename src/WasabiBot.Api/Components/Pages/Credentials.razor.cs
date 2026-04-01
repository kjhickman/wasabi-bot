using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Components.Pages;

public partial class Credentials : ComponentBase
{
    private const int MaxCredentialNameLength = 100;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationStateTask { get; set; }

    [Inject]
    public required IApiCredentialService ApiCredentialService { get; set; }

    private bool IsAuthenticated { get; set; }
    private long? OwnerDiscordUserId { get; set; }
    private IReadOnlyList<ApiCredentialSummary> CredentialRows { get; set; } = [];
    private string? PageError { get; set; }
    private string? CreateCredentialError { get; set; }
    private string? ConfirmError { get; set; }
    private ApiCredentialIssueResult? IssuedCredential { get; set; }

    [SupplyParameterFromQuery(Name = "modal")]
    private string? RequestedModal { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    private long? RequestedCredentialId { get; set; }

    [SupplyParameterFromForm(FormName = "create-credential")]
    private CreateCredentialFormInput? CreateCredentialForm { get; set; }

    [SupplyParameterFromForm(FormName = "confirm-credential")]
    private ConfirmCredentialFormInput? ConfirmCredentialForm { get; set; }

    private string CreateCredentialFormName => CreateCredentialForm?.Name ?? string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        IssuedCredential = null;
        CreateCredentialError = null;
        ConfirmError = null;

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
        await ProcessPostAsync();
    }

    private bool ShowCreateModal => IssuedCredential is null
                                    && (string.Equals(RequestedModal, "create", StringComparison.OrdinalIgnoreCase)
                                        || !string.IsNullOrWhiteSpace(CreateCredentialError));

    private bool ShowConfirmModal => IssuedCredential is null
                                     && CurrentConfirmAction is not null
                                     && ConfirmCredential is not null;

    private PendingCredentialAction? CurrentConfirmAction
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ConfirmCredentialForm?.Intent))
            {
                return ParseAction(ConfirmCredentialForm?.Action);
            }

            return RequestedModal switch
            {
                "delete" => PendingCredentialAction.Delete,
                "regenerate" => PendingCredentialAction.Regenerate,
                _ => null,
            };
        }
    }

    private ApiCredentialSummary? ConfirmCredential => RequestedCredentialId is long requestedCredentialId
        ? CredentialRows.FirstOrDefault(c => c.Id == requestedCredentialId)
        : ConfirmCredentialForm?.CredentialId is long postedCredentialId
            ? CredentialRows.FirstOrDefault(c => c.Id == postedCredentialId)
            : null;

    private long ConfirmCredentialId => ConfirmCredential?.Id ?? RequestedCredentialId ?? ConfirmCredentialForm?.CredentialId ?? 0;

    private string CurrentConfirmActionName => CurrentConfirmAction switch
    {
        PendingCredentialAction.Delete => "delete",
        PendingCredentialAction.Regenerate => "regenerate",
        _ => string.Empty,
    };

    private string ConfirmTitle => CurrentConfirmAction switch
    {
        PendingCredentialAction.Delete => "Delete credential",
        PendingCredentialAction.Regenerate => "Regenerate secret",
        _ => "Confirm action"
    };

    private string ConfirmMessage => CurrentConfirmAction switch
    {
        PendingCredentialAction.Delete => "Delete this credential? Existing access tokens will keep working until they expire.",
        PendingCredentialAction.Regenerate => "Regenerate this secret? The current secret will stop working immediately.",
        _ => string.Empty
    };

    private string ConfirmButtonText => CurrentConfirmAction switch
    {
        PendingCredentialAction.Delete => "Delete",
        PendingCredentialAction.Regenerate => "Regenerate",
        _ => "Confirm"
    };

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

    private async Task ProcessPostAsync()
    {
        if (OwnerDiscordUserId is null)
        {
            return;
        }

        if (string.Equals(CreateCredentialForm?.Intent, "create", StringComparison.Ordinal))
        {
            await CreateCredentialAsync();
            return;
        }

        if (string.Equals(ConfirmCredentialForm?.Intent, "confirm", StringComparison.Ordinal))
        {
            await ConfirmPendingActionAsync();
        }
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
            CreateCredentialForm = null;
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
                    ConfirmCredentialForm = null;
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
                    ConfirmCredentialForm = null;
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

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "Never";
        }

        return value.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
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

    private static PendingCredentialAction? ParseAction(string? value) => value switch
    {
        "delete" => PendingCredentialAction.Delete,
        "regenerate" => PendingCredentialAction.Regenerate,
        _ => null,
    };

    private sealed class CreateCredentialFormInput
    {
        public string? Intent { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ConfirmCredentialFormInput
    {
        public string? Intent { get; set; }
        public string? Action { get; set; }
        public long? CredentialId { get; set; }
    }

    private enum PendingCredentialAction
    {
        Delete,
        Regenerate,
    }
}
