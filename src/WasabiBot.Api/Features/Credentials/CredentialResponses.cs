using System.Text.Json.Serialization;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Features.Credentials;

public sealed record CredentialResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_used_at")] DateTimeOffset? LastUsedAt,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt)
{
    public static CredentialResponse FromSummary(ApiCredentialSummary summary) => new(
        summary.Id,
        summary.Name,
        summary.ClientId,
        summary.CreatedAt,
        summary.LastUsedAt,
        summary.RevokedAt);
}

public sealed record CredentialIssueResponse(
    [property: JsonPropertyName("credential")] CredentialResponse Credential,
    [property: JsonPropertyName("client_secret")] string ClientSecret)
{
    public static CredentialIssueResponse FromResult(ApiCredentialIssueResult result) => new(
        CredentialResponse.FromSummary(result.Credential),
        result.ClientSecret);
}
