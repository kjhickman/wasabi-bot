using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.UnitTests.Infrastructure.Auth;

public class ApiCredentialSecretServiceTests
{
    private static readonly ApiCredentialSecretOptions Options = new("UnitTestPepperMustBeAtLeastThirtyTwo!");

    [Test]
    public async Task CreateClientId_ShouldCreateRandomPrefixedValue()
    {
        var service = new ApiCredentialSecretService(Options);

        var clientId1 = service.CreateClientId();
        var clientId2 = service.CreateClientId();

        await Assert.That(clientId1).StartsWith("wb_");
        await Assert.That(clientId2).StartsWith("wb_");
        await Assert.That(clientId1).IsNotEqualTo(clientId2);
    }

    [Test]
    public async Task CreateClientSecret_ShouldCreateRandomPrefixedValue()
    {
        var service = new ApiCredentialSecretService(Options);

        var secret1 = service.CreateClientSecret();
        var secret2 = service.CreateClientSecret();

        await Assert.That(secret1).StartsWith("wb_secret_");
        await Assert.That(secret2).StartsWith("wb_secret_");
        await Assert.That(secret1).IsNotEqualTo(secret2);
    }

    [Test]
    public async Task VerifySecret_WithMatchingSecret_ReturnsTrue()
    {
        var service = new ApiCredentialSecretService(Options);
        var secret = service.CreateClientSecret();
        var hash = service.HashSecret(secret);

        var verified = service.VerifySecret(secret, hash);

        await Assert.That(verified).IsTrue();
    }

    [Test]
    public async Task VerifySecret_WithDifferentSecret_ReturnsFalse()
    {
        var service = new ApiCredentialSecretService(Options);
        var hash = service.HashSecret("wb_secret_known-value");

        var verified = service.VerifySecret("wb_secret_other-value", hash);

        await Assert.That(verified).IsFalse();
    }

    [Test]
    public async Task VerifySecret_WithInvalidHash_ReturnsFalse()
    {
        var service = new ApiCredentialSecretService(Options);

        var verified = service.VerifySecret("wb_secret_known-value", "not-base64");

        await Assert.That(verified).IsFalse();
    }
}
