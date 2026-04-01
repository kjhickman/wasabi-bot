using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Credentials;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Credentials;

public class DeleteCredentialTests
{
    [Test]
    public async Task Handle_WithOwnedCredential_ReturnsNoContent()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        service.RevokeAsync(123456789, 42, Arg.Any<CancellationToken>()).Returns(true);

        var result = await DeleteCredential.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<NoContent>();
    }

    [Test]
    public async Task Handle_WithUnknownCredential_ReturnsNotFound()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        service.RevokeAsync(123456789, 42, Arg.Any<CancellationToken>()).Returns(false);

        var result = await DeleteCredential.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        var user = ClaimsPrincipalBuilder.Create().AsUnauthenticatedUser().Build();
        var service = Substitute.For<IApiCredentialService>();

        var result = await DeleteCredential.Handle(user, 42, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }
}
