using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Features.ApiCredentials;

namespace WasabiBot.Api.Frontend.Modules.Credentials;

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
    private string? UrlReplacementPath { get; set; }

    [SupplyParameterFromQuery(Name = "modal")]
    private string? RequestedModal { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    private long? RequestedCredentialId { get; set; }

    [SupplyParameterFromForm(FormName = "create-credential")]
    private CredentialsCreateCredentialFormInput? CreateCredentialForm { get; set; }

    [SupplyParameterFromForm(FormName = "confirm-credential")]
    private CredentialsConfirmCredentialFormInput? ConfirmCredentialForm { get; set; }

    private CredentialsCreateCredentialFormInput CurrentCreateCredentialForm => CreateCredentialForm ??= new();

    private CredentialsConfirmCredentialFormInput CurrentConfirmCredentialForm => ConfirmCredentialForm ??= new();

    private string CreateCredentialFormName => CurrentCreateCredentialForm.Name;

    private bool ShowCreateModal => IssuedCredential is null
                                    && (string.Equals(RequestedModal, "create", StringComparison.OrdinalIgnoreCase)
                                        || !string.IsNullOrWhiteSpace(CreateCredentialError));

    private bool ShowConfirmModal => IssuedCredential is null
                                     && CurrentConfirmAction is not null
                                     && ConfirmCredential is not null;

    private PendingCredentialAction? CurrentConfirmAction => RequestedModal switch
    {
        "delete" => PendingCredentialAction.Delete,
        "regenerate" => PendingCredentialAction.Regenerate,
        _ => null,
    };

    private ApiCredentialSummary? ConfirmCredential => RequestedCredentialId is long requestedCredentialId
        ? CredentialRows.FirstOrDefault(c => c.Id == requestedCredentialId)
        : null;

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

    private enum PendingCredentialAction
    {
        Delete,
        Regenerate,
    }
}
