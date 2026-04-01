using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Credentials;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Credentials;

public class ListCredentialsTests
{
    [Test]
    public async Task Handle_WithValidUser_ReturnsOwnedCredentials()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        service.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(
        [
            new ApiCredentialSummary(1, "CLI", "wb_cli", DateTimeOffset.UtcNow, null, null)
        ]);

        var result = await ListCredentials.Handle(user, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<Ok<IEnumerable<CredentialResponse>>>();
        var ok = (Ok<IEnumerable<CredentialResponse>>)result;
        await Assert.That(ok.Value).IsNotNull();
        await Assert.That(ok.Value!.Single().ClientId).IsEqualTo("wb_cli");
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        var user = ClaimsPrincipalBuilder.Create().AsUnauthenticatedUser().Build();
        var service = Substitute.For<IApiCredentialService>();

        var result = await ListCredentials.Handle(user, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
        await service.DidNotReceive().ListAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }
}
