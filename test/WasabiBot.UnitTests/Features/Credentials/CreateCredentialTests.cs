using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Credentials;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Credentials;

public class CreateCredentialTests
{
    [Test]
    public async Task Handle_WithValidUser_ReturnsIssuedCredential()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var request = new CreateCredentialRequest("CLI integration");
        var service = Substitute.For<IApiCredentialService>();
        var createdAt = DateTimeOffset.UtcNow;
        service.CreateAsync(123456789, "CLI integration", Arg.Any<CancellationToken>()).Returns(
            new ApiCredentialIssueResult(
                new ApiCredentialSummary(1, "CLI integration", "wb_cli", createdAt, null, null),
                "wb_secret_value"));

        var result = await CreateCredential.Handle(user, request, service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<Ok<CredentialIssueResponse>>();
        var ok = (Ok<CredentialIssueResponse>)result;
        await Assert.That(ok.Value).IsNotNull();
        await Assert.That(ok.Value!.Credential.Name).IsEqualTo("CLI integration");
        await Assert.That(ok.Value.ClientSecret).IsEqualTo("wb_secret_value");
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        var user = ClaimsPrincipalBuilder.Create().AsUnauthenticatedUser().Build();
        var service = Substitute.For<IApiCredentialService>();

        var result = await CreateCredential.Handle(user, new CreateCredentialRequest("CLI"), service, CancellationToken.None);

        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
        await service.DidNotReceive().CreateAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithInvalidName_ReturnsBadRequest()
    {
        var user = ClaimsPrincipalBuilder.Create().WithUserId("123456789").Build();
        var service = Substitute.For<IApiCredentialService>();
        service.CreateAsync(123456789, "bad", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ApiCredentialIssueResult>(new ArgumentException("Credential name is required.")));

        var result = await CreateCredential.Handle(user, new CreateCredentialRequest("bad"), service, CancellationToken.None);

        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }
}
