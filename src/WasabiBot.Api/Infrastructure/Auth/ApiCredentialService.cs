using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Persistence;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Infrastructure.Auth;

public sealed class ApiCredentialService(WasabiBotContext context, IApiCredentialSecretService secretService) : IApiCredentialService
{
    private const int MaxClientIdAttempts = 5;
    private const int MaxCredentialNameLength = 100;

    public async Task<ApiCredentialSummary[]> ListAsync(long ownerDiscordUserId, CancellationToken cancellationToken = default)
    {
        return await context.ApiCredentials
            .AsNoTracking()
            .Where(c => c.OwnerDiscordUserId == ownerDiscordUserId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => ToSummary(c))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ApiCredentialIssueResult> CreateAsync(long ownerDiscordUserId, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        var clientId = await GenerateUniqueClientIdAsync(cancellationToken);
        var clientSecret = secretService.CreateClientSecret();
        var createdAt = DateTimeOffset.UtcNow;

        var entity = new ApiCredentialEntity
        {
            OwnerDiscordUserId = ownerDiscordUserId,
            Name = normalizedName,
            ClientId = clientId,
            SecretHash = secretService.HashSecret(clientSecret),
            CreatedAt = createdAt,
        };

        context.ApiCredentials.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(entity), clientSecret);
    }

    public async Task<bool> RevokeAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        var credential = await context.ApiCredentials.FirstOrDefaultAsync(
            c => c.Id == credentialId && c.OwnerDiscordUserId == ownerDiscordUserId,
            cancellationToken);

        if (credential is null)
        {
            return false;
        }

        if (credential.RevokedAt is not null)
        {
            return true;
        }

        credential.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ApiCredentialIssueResult?> RegenerateSecretAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        var credential = await context.ApiCredentials.FirstOrDefaultAsync(
            c => c.Id == credentialId && c.OwnerDiscordUserId == ownerDiscordUserId,
            cancellationToken);

        if (credential is null || credential.RevokedAt is not null)
        {
            return null;
        }

        var clientSecret = secretService.CreateClientSecret();
        credential.SecretHash = secretService.HashSecret(clientSecret);
        await context.SaveChangesAsync(cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(credential), clientSecret);
    }

    public async Task<ApiCredentialValidationResult?> ValidateAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var credential = await context.ApiCredentials.FirstOrDefaultAsync(
            c => c.ClientId == clientId,
            cancellationToken);

        if (credential is null || credential.RevokedAt is not null)
        {
            return null;
        }

        if (!secretService.VerifySecret(clientSecret, credential.SecretHash))
        {
            return null;
        }

        credential.LastUsedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new ApiCredentialValidationResult(
            credential.Id,
            credential.OwnerDiscordUserId,
            credential.ClientId,
            credential.Name);
    }

    private async Task<string> GenerateUniqueClientIdAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxClientIdAttempts; attempt++)
        {
            var clientId = secretService.CreateClientId();
            var exists = await context.ApiCredentials
                .AsNoTracking()
                .AnyAsync(c => c.ClientId == clientId, cancellationToken);

            if (!exists)
            {
                return clientId;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique API credential client id.");
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Credential name is required.", nameof(name));
        }

        if (normalizedName.Length > MaxCredentialNameLength)
        {
            throw new ArgumentException($"Credential name must be {MaxCredentialNameLength} characters or fewer.", nameof(name));
        }

        if (normalizedName.Any(char.IsControl))
        {
            throw new ArgumentException("Credential name cannot contain control characters.", nameof(name));
        }

        return normalizedName;
    }

    private static ApiCredentialSummary ToSummary(ApiCredentialEntity credential) => new(
        credential.Id,
        credential.Name,
        credential.ClientId,
        credential.CreatedAt,
        credential.LastUsedAt,
        credential.RevokedAt);
}
