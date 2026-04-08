namespace WasabiBot.Api.Features.ApiCredentials;

public sealed record ApiCredentialSummary(
    long Id,
    string Name,
    string ClientId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record ApiCredentialIssueResult(
    ApiCredentialSummary Credential,
    string ClientSecret);

public sealed record ApiCredentialValidationResult(
    long CredentialId,
    long OwnerDiscordUserId,
    string ClientId,
    string Name);
