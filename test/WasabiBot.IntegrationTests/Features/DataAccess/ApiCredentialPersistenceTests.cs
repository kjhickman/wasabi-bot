using Dapper;
using Npgsql;
using WasabiBot.Api.Persistence.Entities;
using WasabiBot.IntegrationTests.Infrastructure;

namespace WasabiBot.IntegrationTests.Features.DataAccess;

public class ApiCredentialPersistenceTests : IntegrationTestBase
{
    [Test]
    public async Task Insert_ShouldPersistApiCredential()
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var lastUsedAt = createdAt.AddMinutes(5);

        await InsertCredentialAsync(connection, new ApiCredentialEntity
        {
            OwnerDiscordUserId = 123456789,
            Name = "CLI integration",
            ClientId = "wb_client_123",
            SecretHash = "hashed-secret",
            CreatedAt = createdAt,
            LastUsedAt = lastUsedAt,
        });

        var credential = await connection.QuerySingleAsync<ApiCredentialRow>("""
            SELECT "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
            FROM "ApiCredentials"
            """);

        await Assert.That(credential.OwnerDiscordUserId).IsEqualTo(123456789L);
        await Assert.That(credential.Name).IsEqualTo("CLI integration");
        await Assert.That(credential.ClientId).IsEqualTo("wb_client_123");
        await Assert.That(credential.SecretHash).IsEqualTo("hashed-secret");
        await Assert.That(PostgresTimestamp.Normalize(credential.CreatedAt)).IsEqualTo(PostgresTimestamp.Normalize(createdAt));
        await Assert.That(PostgresTimestamp.Normalize(credential.LastUsedAt)).IsEqualTo(PostgresTimestamp.Normalize(lastUsedAt));
        await Assert.That(credential.RevokedAt).IsNull();
    }

    [Test]
    public async Task Insert_ShouldEnforceUniqueClientId()
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync();
        var createdAt = DateTimeOffset.UtcNow;

        await InsertCredentialAsync(connection, new ApiCredentialEntity
        {
            OwnerDiscordUserId = 111,
            Name = "First",
            ClientId = "duplicate-client-id",
            SecretHash = "hash-1",
            CreatedAt = createdAt,
        });

        await Assert.That(async () => await InsertCredentialAsync(connection, new ApiCredentialEntity
            {
                OwnerDiscordUserId = 222,
                Name = "Second",
                ClientId = "duplicate-client-id",
                SecretHash = "hash-2",
                CreatedAt = createdAt,
            }))
            .Throws<NpgsqlException>();
    }

    [Test]
    public async Task Query_ShouldFilterByOwnerDiscordUserId()
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync();
        var createdAt = DateTimeOffset.UtcNow;

        await InsertCredentialAsync(connection, new ApiCredentialEntity
        {
            OwnerDiscordUserId = 777,
            Name = "Owner one",
            ClientId = "owner-one-client",
            SecretHash = "hash-1",
            CreatedAt = createdAt,
        });
        await InsertCredentialAsync(connection, new ApiCredentialEntity
        {
            OwnerDiscordUserId = 777,
            Name = "Owner one second",
            ClientId = "owner-one-client-2",
            SecretHash = "hash-2",
            CreatedAt = createdAt,
            RevokedAt = createdAt.AddHours(1),
        });
        await InsertCredentialAsync(connection, new ApiCredentialEntity
        {
            OwnerDiscordUserId = 888,
            Name = "Owner two",
            ClientId = "owner-two-client",
            SecretHash = "hash-3",
            CreatedAt = createdAt,
        });

        var ownerCredentials = (await connection.QueryAsync<ApiCredentialRow>("""
            SELECT "OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt"
            FROM "ApiCredentials"
            WHERE "OwnerDiscordUserId" = @OwnerDiscordUserId
            ORDER BY "Id"
            """, new { OwnerDiscordUserId = 777L })).ToArray();

        await Assert.That(ownerCredentials.Length).IsEqualTo(2);
        await Assert.That(ownerCredentials[0].ClientId).IsEqualTo("owner-one-client");
        await Assert.That(ownerCredentials[1].ClientId).IsEqualTo("owner-one-client-2");
        await Assert.That(ownerCredentials[1].RevokedAt).IsNotNull();
    }

    private static Task InsertCredentialAsync(NpgsqlConnection connection, ApiCredentialEntity credential)
    {
        return connection.ExecuteAsync("""
            INSERT INTO "ApiCredentials" ("OwnerDiscordUserId", "Name", "ClientId", "SecretHash", "CreatedAt", "LastUsedAt", "RevokedAt")
            VALUES (@OwnerDiscordUserId, @Name, @ClientId, @SecretHash, @CreatedAt, @LastUsedAt, @RevokedAt)
            """, credential);
    }

    private sealed class ApiCredentialRow
    {
        public long OwnerDiscordUserId { get; set; }
        public required string Name { get; set; }
        public required string ClientId { get; set; }
        public required string SecretHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
