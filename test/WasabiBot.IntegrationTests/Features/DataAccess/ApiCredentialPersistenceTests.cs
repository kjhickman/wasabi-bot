using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Persistence.Entities;
using WasabiBot.IntegrationTests.Infrastructure;

namespace WasabiBot.IntegrationTests.Features.DataAccess;

public class ApiCredentialPersistenceTests : IntegrationTestBase
{
    [Test]
    public async Task SaveChangesAsync_ShouldPersistApiCredential()
    {
        await using var context = CreateContext();
        var createdAt = DateTimeOffset.UtcNow;
        var lastUsedAt = createdAt.AddMinutes(5);

        context.ApiCredentials.Add(new ApiCredentialEntity
        {
            OwnerDiscordUserId = 123456789,
            Name = "CLI integration",
            ClientId = "wb_client_123",
            SecretHash = "hashed-secret",
            CreatedAt = createdAt,
            LastUsedAt = lastUsedAt,
        });

        await context.SaveChangesAsync();

        await using var assertContext = CreateContext();
        var credential = await assertContext.ApiCredentials.SingleAsync();

        await Assert.That(credential.OwnerDiscordUserId).IsEqualTo(123456789L);
        await Assert.That(credential.Name).IsEqualTo("CLI integration");
        await Assert.That(credential.ClientId).IsEqualTo("wb_client_123");
        await Assert.That(credential.SecretHash).IsEqualTo("hashed-secret");
        await Assert.That(PostgresTimestamp.Normalize(credential.CreatedAt)).IsEqualTo(PostgresTimestamp.Normalize(createdAt));
        await Assert.That(PostgresTimestamp.Normalize(credential.LastUsedAt)).IsEqualTo(PostgresTimestamp.Normalize(lastUsedAt));
        await Assert.That(credential.RevokedAt).IsNull();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldEnforceUniqueClientId()
    {
        await using var context = CreateContext();
        var createdAt = DateTimeOffset.UtcNow;

        context.ApiCredentials.AddRange(
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 111,
                Name = "First",
                ClientId = "duplicate-client-id",
                SecretHash = "hash-1",
                CreatedAt = createdAt,
            },
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 222,
                Name = "Second",
                ClientId = "duplicate-client-id",
                SecretHash = "hash-2",
                CreatedAt = createdAt,
            });

        await Assert.That(async () => await context.SaveChangesAsync())
            .Throws<DbUpdateException>();
    }

    [Test]
    public async Task Query_ShouldFilterByOwnerDiscordUserId()
    {
        await using var context = CreateContext();
        var createdAt = DateTimeOffset.UtcNow;

        context.ApiCredentials.AddRange(
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 777,
                Name = "Owner one",
                ClientId = "owner-one-client",
                SecretHash = "hash-1",
                CreatedAt = createdAt,
            },
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 777,
                Name = "Owner one second",
                ClientId = "owner-one-client-2",
                SecretHash = "hash-2",
                CreatedAt = createdAt,
                RevokedAt = createdAt.AddHours(1),
            },
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 888,
                Name = "Owner two",
                ClientId = "owner-two-client",
                SecretHash = "hash-3",
                CreatedAt = createdAt,
            });

        await context.SaveChangesAsync();

        await using var assertContext = CreateContext();
        var ownerCredentials = await assertContext.ApiCredentials
            .Where(c => c.OwnerDiscordUserId == 777)
            .OrderBy(c => c.Id)
            .ToListAsync();

        await Assert.That(ownerCredentials.Count).IsEqualTo(2);
        await Assert.That(ownerCredentials[0].ClientId).IsEqualTo("owner-one-client");
        await Assert.That(ownerCredentials[1].ClientId).IsEqualTo("owner-one-client-2");
        await Assert.That(ownerCredentials[1].RevokedAt).IsNotNull();
    }
}
