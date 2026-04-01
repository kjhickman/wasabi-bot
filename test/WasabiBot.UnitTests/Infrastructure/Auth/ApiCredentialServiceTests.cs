using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Persistence;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.UnitTests.Infrastructure.Auth;

public class ApiCredentialServiceTests
{
    [Test]
    public async Task CreateAsync_ShouldPersistCredentialAndReturnSecret()
    {
        await using var context = CreateContext();
        var secretService = CreateSecretService();
        var service = new ApiCredentialService(context, secretService);

        var result = await service.CreateAsync(123456789, " CLI integration ");

        await Assert.That(result.ClientSecret).IsEqualTo("secret-1");
        await Assert.That(result.Credential.Name).IsEqualTo("CLI integration");
        await Assert.That(result.Credential.ClientId).IsEqualTo("wb_client_1");

        var credential = await context.ApiCredentials.SingleAsync();
        await Assert.That(credential.OwnerDiscordUserId).IsEqualTo(123456789L);
        await Assert.That(credential.SecretHash).IsEqualTo("hash-secret-1");
    }

    [Test]
    public async Task CreateAsync_ShouldRetryWhenGeneratedClientIdAlreadyExists()
    {
        await using var context = CreateContext();
        context.ApiCredentials.Add(new ApiCredentialEntity
        {
            OwnerDiscordUserId = 1,
            Name = "Existing",
            ClientId = "wb_collision",
            SecretHash = "hash-existing",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var secretService = Substitute.For<IApiCredentialSecretService>();
        secretService.CreateClientId().Returns("wb_collision", "wb_unique");
        secretService.CreateClientSecret().Returns("secret-1");
        secretService.HashSecret("secret-1").Returns("hash-secret-1");

        var service = new ApiCredentialService(context, secretService);

        var result = await service.CreateAsync(2, "New credential");

        await Assert.That(result.Credential.ClientId).IsEqualTo("wb_unique");
        await Assert.That(await context.ApiCredentials.CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task ListAsync_ShouldReturnOnlyActiveCredentialsForOwner()
    {
        await using var context = CreateContext();
        context.ApiCredentials.AddRange(
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 7,
                Name = "First",
                ClientId = "wb_one",
                SecretHash = "hash-1",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            },
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 8,
                Name = "Other owner",
                ClientId = "wb_other",
                SecretHash = "hash-2",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            new ApiCredentialEntity
            {
                OwnerDiscordUserId = 7,
                Name = "Second",
                ClientId = "wb_two",
                SecretHash = "hash-3",
                CreatedAt = DateTimeOffset.UtcNow,
                RevokedAt = DateTimeOffset.UtcNow,
            });
        await context.SaveChangesAsync();

        var service = new ApiCredentialService(context, CreateSecretService());

        var credentials = await service.ListAsync(7);

        await Assert.That(credentials.Length).IsEqualTo(1);
        await Assert.That(credentials[0].ClientId).IsEqualTo("wb_one");
    }

    [Test]
    public async Task RevokeAsync_ShouldOnlyRevokeOwnedCredential()
    {
        await using var context = CreateContext();
        var credential = new ApiCredentialEntity
        {
            OwnerDiscordUserId = 55,
            Name = "Owned credential",
            ClientId = "wb_owned",
            SecretHash = "hash-1",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.ApiCredentials.Add(credential);
        await context.SaveChangesAsync();

        var service = new ApiCredentialService(context, CreateSecretService());

        var notOwnedResult = await service.RevokeAsync(99, credential.Id);
        var ownedResult = await service.RevokeAsync(55, credential.Id);

        await Assert.That(notOwnedResult).IsFalse();
        await Assert.That(ownedResult).IsTrue();
        await Assert.That((await context.ApiCredentials.SingleAsync()).RevokedAt).IsNotNull();
    }

    [Test]
    public async Task RegenerateSecretAsync_ShouldReplaceSecretForOwnedActiveCredential()
    {
        await using var context = CreateContext();
        var credential = new ApiCredentialEntity
        {
            OwnerDiscordUserId = 55,
            Name = "Owned credential",
            ClientId = "wb_owned",
            SecretHash = "hash-old",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.ApiCredentials.Add(credential);
        await context.SaveChangesAsync();

        var secretService = CreateSecretService();
        var service = new ApiCredentialService(context, secretService);

        var result = await service.RegenerateSecretAsync(55, credential.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ClientSecret).IsEqualTo("secret-1");
        await Assert.That((await context.ApiCredentials.SingleAsync()).SecretHash).IsEqualTo("hash-secret-1");
    }

    [Test]
    public async Task RegenerateSecretAsync_ShouldReturnNullForRevokedCredential()
    {
        await using var context = CreateContext();
        var credential = new ApiCredentialEntity
        {
            OwnerDiscordUserId = 55,
            Name = "Owned credential",
            ClientId = "wb_owned",
            SecretHash = "hash-old",
            CreatedAt = DateTimeOffset.UtcNow,
            RevokedAt = DateTimeOffset.UtcNow,
        };
        context.ApiCredentials.Add(credential);
        await context.SaveChangesAsync();

        var service = new ApiCredentialService(context, CreateSecretService());

        var result = await service.RegenerateSecretAsync(55, credential.Id);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnOwnerDataAndUpdateLastUsedAt()
    {
        await using var context = CreateContext();
        var credential = new ApiCredentialEntity
        {
            OwnerDiscordUserId = 123,
            Name = "Owned credential",
            ClientId = "wb_owned",
            SecretHash = "hash-secret-1",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.ApiCredentials.Add(credential);
        await context.SaveChangesAsync();

        var secretService = CreateSecretService();
        secretService.VerifySecret("secret-1", "hash-secret-1").Returns(true);
        var service = new ApiCredentialService(context, secretService);

        var result = await service.ValidateAsync("wb_owned", "secret-1");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.OwnerDiscordUserId).IsEqualTo(123L);
        await Assert.That((await context.ApiCredentials.SingleAsync()).LastUsedAt).IsNotNull();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNullForInvalidSecret()
    {
        await using var context = CreateContext();
        context.ApiCredentials.Add(new ApiCredentialEntity
        {
            OwnerDiscordUserId = 123,
            Name = "Owned credential",
            ClientId = "wb_owned",
            SecretHash = "hash-secret-1",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var secretService = CreateSecretService();
        secretService.VerifySecret("wrong-secret", "hash-secret-1").Returns(false);
        var service = new ApiCredentialService(context, secretService);

        var result = await service.ValidateAsync("wb_owned", "wrong-secret");

        await Assert.That(result).IsNull();
        await Assert.That((await context.ApiCredentials.SingleAsync()).LastUsedAt).IsNull();
    }

    [Test]
    public async Task CreateAsync_WithBlankName_ShouldThrow()
    {
        await using var context = CreateContext();
        var service = new ApiCredentialService(context, CreateSecretService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, "   "));
    }

    [Test]
    public async Task CreateAsync_WithControlCharactersInName_ShouldThrow()
    {
        await using var context = CreateContext();
        var service = new ApiCredentialService(context, CreateSecretService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, "CLI\nIntegration"));
    }

    [Test]
    public async Task CreateAsync_WithNameLongerThan100Characters_ShouldThrow()
    {
        await using var context = CreateContext();
        var service = new ApiCredentialService(context, CreateSecretService());
        var longName = new string('a', 101);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, longName));
    }

    private static WasabiBotContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WasabiBotContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WasabiBotContext(options);
    }

    private static IApiCredentialSecretService CreateSecretService()
    {
        var secretService = Substitute.For<IApiCredentialSecretService>();
        secretService.CreateClientId().Returns("wb_client_1");
        secretService.CreateClientSecret().Returns("secret-1");
        secretService.HashSecret("secret-1").Returns("hash-secret-1");
        secretService.VerifySecret(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        return secretService;
    }
}
