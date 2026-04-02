using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.OAuth;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.UnitTests.Features.OAuth;

public class GetOAuthTokenTests
{
    private static ApiTokenFactory CreateTokenFactory(TimeSpan? lifetime = null)
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var signingKey = new SymmetricSecurityKey(key);
        var options = new ApiTokenOptions(signingKey, lifetime ?? TimeSpan.FromHours(1));
        return new ApiTokenFactory(options);
    }

    [Test]
    public async Task Handle_WithValidClientCredentials_ReturnsTokenResponse()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, StringValues>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "wb_cli",
            ["client_secret"] = "wb_secret"
        });
        var credentialService = Substitute.For<IApiCredentialService>();
        credentialService.ValidateAsync("wb_cli", "wb_secret", Arg.Any<CancellationToken>())
            .Returns(new ApiCredentialValidationResult(1, 123456789, "wb_cli", "CLI integration"));
        var tokenFactory = CreateTokenFactory(TimeSpan.FromMinutes(30));

        var result = await GetOAuthToken.Handle(httpContext, credentialService, tokenFactory);

        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var ok = (Ok<TokenResponse>)result;
        await Assert.That(ok.Value).IsNotNull();
        await Assert.That(ok.Value!.AccessToken).IsNotEmpty();
        await Assert.That(ok.Value.TokenType).IsEqualTo("Bearer");
        await Assert.That(ok.Value.ExpiresIn).IsEqualTo(1800);
    }

    [Test]
    public async Task Handle_WithUnsupportedGrantType_ReturnsBadRequest()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, StringValues>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "wb_cli",
            ["client_secret"] = "wb_secret"
        });
        var credentialService = Substitute.For<IApiCredentialService>();

        var result = await GetOAuthToken.Handle(httpContext, credentialService, CreateTokenFactory());

        await Assert.That(result.GetType().Name).Contains("BadRequest");
        await credentialService.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithMissingClientId_ReturnsBadRequest()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, StringValues>
        {
            ["grant_type"] = "client_credentials",
            ["client_secret"] = "wb_secret"
        });
        var credentialService = Substitute.For<IApiCredentialService>();

        var result = await GetOAuthToken.Handle(httpContext, credentialService, CreateTokenFactory());

        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithInvalidClientCredentials_ReturnsBadRequest()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, StringValues>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "wb_cli",
            ["client_secret"] = "wrong"
        });
        var credentialService = Substitute.For<IApiCredentialService>();
        credentialService.ValidateAsync("wb_cli", "wrong", Arg.Any<CancellationToken>())
            .Returns((ApiCredentialValidationResult?)null);

        var result = await GetOAuthToken.Handle(httpContext, credentialService, CreateTokenFactory());

        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_AddsNoStoreHeadersToResponses()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, StringValues>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "wb_cli",
            ["client_secret"] = "wb_secret"
        });
        var credentialService = Substitute.For<IApiCredentialService>();
        credentialService.ValidateAsync("wb_cli", "wb_secret", Arg.Any<CancellationToken>())
            .Returns(new ApiCredentialValidationResult(1, 123456789, "wb_cli", "CLI integration"));

        await GetOAuthToken.Handle(httpContext, credentialService, CreateTokenFactory());

        await Assert.That(httpContext.Response.Headers.CacheControl.ToString()).IsEqualTo("no-store, no-cache, max-age=0");
        await Assert.That(httpContext.Response.Headers.Pragma.ToString()).IsEqualTo("no-cache");
        await Assert.That(httpContext.Response.Headers.Expires.ToString()).IsEqualTo("0");
    }

    private static DefaultHttpContext CreateHttpContext(Dictionary<string, StringValues> formValues)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(TracerProvider.Default.GetTracer("oauth-tests"))
            .BuildServiceProvider();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(formValues);
        return httpContext;
    }
}
