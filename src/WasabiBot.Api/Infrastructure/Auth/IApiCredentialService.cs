namespace WasabiBot.Api.Infrastructure.Auth;

public interface IApiCredentialService
{
    Task<ApiCredentialSummary[]> ListAsync(long ownerDiscordUserId, CancellationToken cancellationToken = default);
    Task<ApiCredentialIssueResult> CreateAsync(long ownerDiscordUserId, string name, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default);
    Task<ApiCredentialIssueResult?> RegenerateSecretAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default);
    Task<ApiCredentialValidationResult?> ValidateAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default);
}
