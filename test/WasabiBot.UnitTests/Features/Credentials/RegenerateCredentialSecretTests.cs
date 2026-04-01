using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Credentials;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Credentials;

public class RegenerateCredentialSecretTests
{
    [Test]
    public async Task Handle_WithOwnedCredential_ReturnsNewSecret()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        var createdAt = DateTimeOffset.UtcNow;
        service.RegenerateSecretAsync(123456789, 42, Arg.Any<CancellationToken>()).Returns(
            new ApiCredentialIssueResult(
                new ApiCredentialSummary(42, "CLI", "wb_cli", createdAt, null, null),
                "wb_secret_new"));

        var result = await RegenerateCredentialSecret.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<Ok<CredentialIssueResponse>>();
        var ok = (Ok<CredentialIssueResponse>)result;
        await Assert.That(ok.Value).IsNotNull();
        await Assert.That(ok.Value!.ClientSecret).IsEqualTo("wb_secret_new");
    }

    [Test]
    public async Task Handle_WithUnknownCredential_ReturnsNotFound()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        service.RegenerateSecretAsync(123456789, 42, Arg.Any<CancellationToken>()).Returns((ApiCredentialIssueResult?)null);

        var result = await RegenerateCredentialSecret.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        var user = ClaimsPrincipalBuilder.Create().AsUnauthenticatedUser().Build();
        var service = Substitute.For<IApiCredentialService>();

        var result = await RegenerateCredentialSecret.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }
}
