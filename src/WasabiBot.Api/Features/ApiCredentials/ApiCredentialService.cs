using Dapper;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.ApiCredentials;

public sealed class ApiCredentialService(
    NpgsqlDataSource dataSource,
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
        span.SetAttribute("auth.owner_discord_user_id", ownerDiscordUserId.ToString());

        return await cache.GetOrCreateAsync(
            GetListCacheKey(ownerDiscordUserId),
            async cancel =>
            {
                const string sql = """
                    SELECT "Id", "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
                    FROM "ApiCredentials"
                    WHERE "OwnerDiscordUserId" = @OwnerDiscordUserId AND "RevokedAt" IS NULL
                    ORDER BY "CreatedAt" DESC, "Id" DESC
                    """;

                await using var connection = await dataSource.OpenConnectionAsync(cancel);
                var credentials = await connection.QueryAsync<ApiCredentialRow>(new CommandDefinition(
                    sql, new { OwnerDiscordUserId = ownerDiscordUserId }, cancellationToken: cancel));
                return credentials.Select(row => ToSummary(row.ToEntity())).ToArray();
            },
            _listCacheOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<ApiCredentialIssueResult> CreateAsync(long ownerDiscordUserId, string name, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.create");
        span.SetAttribute("auth.owner_discord_user_id", ownerDiscordUserId.ToString());

        var normalizedName = NormalizeName(name);
        var clientId = await GenerateUniqueClientIdAsync(cancellationToken);
        var clientSecret = secretService.CreateClientSecret();
        var createdAt = DateTimeOffset.UtcNow;

        const string sql = """
            INSERT INTO "ApiCredentials" ("OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt")
            VALUES (@OwnerDiscordUserId, @Name, @ClientId, @SecretHash, @CreatedAt)
            RETURNING "Id", "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var entity = (await connection.QuerySingleAsync<ApiCredentialRow>(new CommandDefinition(sql, new
        {
            OwnerDiscordUserId = ownerDiscordUserId,
            Name = normalizedName,
            ClientId = clientId,
            SecretHash = secretService.HashSecret(clientSecret),
            CreatedAt = createdAt,
        }, cancellationToken: cancellationToken))).ToEntity();
        await cache.RemoveAsync(GetListCacheKey(ownerDiscordUserId), cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(entity), clientSecret);
    }

    public async Task<bool> RevokeAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.revoke");
        span.SetAttribute("auth.owner_discord_user_id", ownerDiscordUserId.ToString());
        span.SetAttribute("auth.credential_id", credentialId);

        const string selectSql = """
            SELECT "Id", "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
            FROM "ApiCredentials"
            WHERE "Id" = @CredentialId AND "OwnerDiscordUserId" = @OwnerDiscordUserId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var credential = (await connection.QueryFirstOrDefaultAsync<ApiCredentialRow>(new CommandDefinition(
            selectSql, new { CredentialId = credentialId, OwnerDiscordUserId = ownerDiscordUserId }, cancellationToken: cancellationToken)))?.ToEntity();

        if (credential is null)
        {
            return false;
        }

        if (credential.RevokedAt is not null)
        {
            return true;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"ApiCredentials\" SET \"RevokedAt\" = @RevokedAt WHERE \"Id\" = @CredentialId",
            new { RevokedAt = DateTimeOffset.UtcNow, CredentialId = credentialId }, cancellationToken: cancellationToken));
        await cache.RemoveAsync(GetValidationCacheKey(credential.ClientId), cancellationToken);
        await cache.RemoveAsync(GetListCacheKey(ownerDiscordUserId), cancellationToken);
        return true;
    }

    public async Task<ApiCredentialIssueResult?> RegenerateSecretAsync(long ownerDiscordUserId, long credentialId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.regenerate-secret");
        span.SetAttribute("auth.owner_discord_user_id", ownerDiscordUserId.ToString());
        span.SetAttribute("auth.credential_id", credentialId);

        const string selectSql = """
            SELECT "Id", "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
            FROM "ApiCredentials"
            WHERE "Id" = @CredentialId AND "OwnerDiscordUserId" = @OwnerDiscordUserId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var credential = (await connection.QueryFirstOrDefaultAsync<ApiCredentialRow>(new CommandDefinition(
            selectSql, new { CredentialId = credentialId, OwnerDiscordUserId = ownerDiscordUserId }, cancellationToken: cancellationToken)))?.ToEntity();

        if (credential is null || credential.RevokedAt is not null)
        {
            return null;
        }

        var clientSecret = secretService.CreateClientSecret();
        credential.SecretHash = secretService.HashSecret(clientSecret);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"ApiCredentials\" SET \"SecretHash\" = @SecretHash WHERE \"Id\" = @CredentialId",
            new { credential.SecretHash, CredentialId = credentialId }, cancellationToken: cancellationToken));
        await cache.RemoveAsync(GetValidationCacheKey(credential.ClientId), cancellationToken);

        return new ApiCredentialIssueResult(ToSummary(credential), clientSecret);
    }

    public async Task<ApiCredentialValidationResult?> ValidateAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.api-credential.validate");
        span.SetAttribute("auth.client_id.present", !string.IsNullOrWhiteSpace(clientId));

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var credential = await cache.GetOrCreateAsync(
            GetValidationCacheKey(clientId),
            async cancel =>
            {
                const string sql = """
                    SELECT "Id", "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
                    FROM "ApiCredentials"
                    WHERE "ClientId" = @ClientId
                    """;

                await using var connection = await dataSource.OpenConnectionAsync(cancel);
                var entity = (await connection.QueryFirstOrDefaultAsync<ApiCredentialRow>(new CommandDefinition(
                    sql, new { ClientId = clientId }, cancellationToken: cancel)))?.ToEntity();

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

        await using var validateConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        var entity = (await validateConnection.QueryFirstOrDefaultAsync<ApiCredentialRow>(new CommandDefinition(
            "SELECT \"Id\", \"OwnerDiscordUserId\", \"Name\", \"ClientId\", \"SecretHash\", \"CreatedAt\", \"LastUsedAt\", \"RevokedAt\" FROM \"ApiCredentials\" WHERE \"Id\" = @Id",
            new { credential.Id }, cancellationToken: cancellationToken)))?.ToEntity();
        if (entity is null || entity.RevokedAt is not null)
        {
            await cache.RemoveAsync(GetValidationCacheKey(clientId), cancellationToken);
            return null;
        }

        await validateConnection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"ApiCredentials\" SET \"LastUsedAt\" = @LastUsedAt WHERE \"Id\" = @Id",
            new { LastUsedAt = DateTimeOffset.UtcNow, entity.Id }, cancellationToken: cancellationToken));

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
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM \"ApiCredentials\" WHERE \"ClientId\" = @ClientId)",
                new { ClientId = clientId }, cancellationToken: cancellationToken));

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

    private sealed class ApiCredentialRow
    {
        public long Id { get; set; }
        public long OwnerDiscordUserId { get; set; }
        public required string Name { get; set; }
        public required string ClientId { get; set; }
        public required string SecretHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public ApiCredentialEntity ToEntity() => new()
        {
            Id = Id,
            OwnerDiscordUserId = OwnerDiscordUserId,
            Name = Name,
            ClientId = ClientId,
            SecretHash = SecretHash,
            CreatedAt = ToDateTimeOffset(CreatedAt),
            LastUsedAt = LastUsedAt is null ? null : ToDateTimeOffset(LastUsedAt.Value),
            RevokedAt = RevokedAt is null ? null : ToDateTimeOffset(RevokedAt.Value),
        };

        private static DateTimeOffset ToDateTimeOffset(DateTime value) => new(value.ToUniversalTime());
    }

}
