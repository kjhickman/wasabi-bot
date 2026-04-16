using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Database.Entities;

namespace WasabiBot.Api.Features.ApiCredentials;

public sealed class ApiCredentialService(
    WasabiBotContext context,
    IApiCredentialSecretService secretService,
    HybridCache cache,
    Tracer tracer) : IApiCredentialService
{
    private const int MaxClientIdAttempts = 5;
    private const int MaxCredentialNameLength = 100;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

    private readonly HybridCacheEntryOptions _listCacheOptions = new()
    {
        Expiration = CacheExpiration,
        LocalCacheExpiration = CacheExpiration
    };

    private readonly HybridCacheEntryOptions _validationCacheOptions = new()
    {
        Expiration = CacheExpiration,
        LocalCacheExpiration = CacheExpiration
    };

    public async Task<ApiCredentialSummary[]> ListAsync(long ownerDiscordUserId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.list");

        return await cache.GetOrCreateAsync(
            GetListCacheKey(ownerDiscordUserId),
            async cancel => await context.ApiCredentials
                .AsNoTracking()
                .Where(c => c.OwnerDiscordUserId == ownerDiscordUserId && c.RevokedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Select(c => ToSummary(c))
                .ToArrayAsync(cancel),
            _listCacheOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<ApiCredentialIssueResult> CreateAsync(long ownerDiscordUserId, string name, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.create");

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
        await cache.RemoveAsync(GetListCacheKey(ownerDiscordUserId), cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(entity), clientSecret);
    }

    public async Task<bool> RevokeAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.revoke");

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
        await cache.RemoveAsync(GetValidationCacheKey(credential.ClientId), cancellationToken);
        await cache.RemoveAsync(GetListCacheKey(ownerDiscordUserId), cancellationToken);
        return true;
    }

    public async Task<ApiCredentialIssueResult?> RegenerateSecretAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.regenerate-secret");

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
        await cache.RemoveAsync(GetValidationCacheKey(credential.ClientId), cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(credential), clientSecret);
    }

    public async Task<ApiCredentialValidationResult?> ValidateAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.validate");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var credential = await cache.GetOrCreateAsync(
            GetValidationCacheKey(clientId),
            async cancel =>
            {
                var entity = await context.ApiCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClientId == clientId, cancel);

                return entity is null
                    ? null
                    : new CachedApiCredential(
                        entity.Id,
                        entity.OwnerDiscordUserId,
                        entity.ClientId,
                        entity.Name,
                        entity.SecretHash,
                        entity.RevokedAt);
            },
            _validationCacheOptions,
            cancellationToken: cancellationToken);

        if (credential is null || credential.RevokedAt is not null)
        {
            return null;
        }

        if (!secretService.VerifySecret(clientSecret, credential.SecretHash))
        {
            return null;
        }

        var entity = await context.ApiCredentials.FirstOrDefaultAsync(c => c.Id == credential.Id, cancellationToken);
        if (entity is null || entity.RevokedAt is not null)
        {
            await cache.RemoveAsync(GetValidationCacheKey(clientId), cancellationToken);
            return null;
        }

        entity.LastUsedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new ApiCredentialValidationResult(
            entity.Id,
            credential.OwnerDiscordUserId,
            credential.ClientId,
            credential.Name);
    }

    private async Task<string> GenerateUniqueClientIdAsync(CancellationToken cancellationToken)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.generate-client-id");

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

    private static string GetListCacheKey(long ownerDiscordUserId) => $"api-credential:list:{ownerDiscordUserId}";

    private static string GetValidationCacheKey(string clientId) => $"api-credential:validate:{clientId}";

    private sealed record CachedApiCredential(
        long Id,
        long OwnerDiscordUserId,
        string ClientId,
        string Name,
        string SecretHash,
        DateTimeOffset? RevokedAt);
}
