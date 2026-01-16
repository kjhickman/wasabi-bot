using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Features.Token;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Token;

public class GetTokenTests
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
    public async Task Handle_WithValidUser_ReturnsOkWithToken()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        await Assert.That(okResult.Value).IsNotNull();
        await Assert.That(okResult.Value!.Token).IsNotEmpty();
    }

    [Test]
    public async Task Handle_WithValidUser_IncludesExpirationTime()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var lifetime = TimeSpan.FromHours(2);
        var tokenFactory = CreateTokenFactory(lifetime);
        var beforeRequest = DateTimeOffset.UtcNow;

        // Act
        var result = GetToken.Handle(user, tokenFactory);
        var afterRequest = DateTimeOffset.UtcNow;

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        var response = okResult.Value!;

        // Verify the expiration is approximately current time + lifetime
        var expectedMinExpiration = beforeRequest.Add(lifetime).AddSeconds(-2);
        var expectedMaxExpiration = afterRequest.Add(lifetime).AddSeconds(2);

        await Assert.That(response.ExpiresAt).IsGreaterThanOrEqualTo(expectedMinExpiration);
        await Assert.That(response.ExpiresAt).IsLessThanOrEqualTo(expectedMaxExpiration);
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsUnauthenticatedUser()
            .WithName("testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsCorrectErrorMessage()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsUnauthenticatedUser()
            .WithName("testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");

        // Use dynamic to access Value property
        dynamic badRequestResult = result;
        var errorObj = badRequestResult.Value;
        await Assert.That((object?)errorObj).IsNotNull();

        // Use reflection to get the error property from the anonymous type
        var errorProperty = errorObj.GetType().GetProperty("error");
        await Assert.That((object?)errorProperty).IsNotNull();
        var errorValue = errorProperty!.GetValue(errorObj) as string;
        await Assert.That(errorValue).IsEqualTo("missing_user_id");
    }

    [Test]
    public async Task Handle_WithEmptyUserId_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("")
            .WithName("testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithWhitespaceUserId_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("   ")
            .WithName("testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithValidUser_TokenIsNotEmpty()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        var token = okResult.Value!.Token;

        await Assert.That(token).IsNotNull();
        await Assert.That(token).IsNotEmpty();
        await Assert.That(token.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Handle_WithValidUser_CreatesJwtToken()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        var token = okResult.Value!.Token;

        // JWT tokens have three parts separated by dots
        var parts = token.Split('.');
        await Assert.That(parts.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Handle_WithDifferentLifetimes_ReturnsCorrectExpiration()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var lifetime = TimeSpan.FromMinutes(30);
        var tokenFactory = CreateTokenFactory(lifetime);
        var beforeRequest = DateTimeOffset.UtcNow;

        // Act
        var result = GetToken.Handle(user, tokenFactory);
        var afterRequest = DateTimeOffset.UtcNow;

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        var response = okResult.Value!;

        var expectedMinExpiration = beforeRequest.Add(lifetime).AddSeconds(-2);
        var expectedMaxExpiration = afterRequest.Add(lifetime).AddSeconds(2);

        await Assert.That(response.ExpiresAt).IsGreaterThanOrEqualTo(expectedMinExpiration);
        await Assert.That(response.ExpiresAt).IsLessThanOrEqualTo(expectedMaxExpiration);
    }

    [Test]
    public async Task Handle_MultipleCalls_CreatesDifferentTokens()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result1 = GetToken.Handle(user, tokenFactory);
        var result2 = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result1).IsTypeOf<Ok<TokenResponse>>();
        await Assert.That(result2).IsTypeOf<Ok<TokenResponse>>();

        var token1 = ((Ok<TokenResponse>)result1).Value!.Token;
        var token2 = ((Ok<TokenResponse>)result2).Value!.Token;

        // Tokens should be different due to different JTI (unique identifier)
        await Assert.That(token1).IsNotEqualTo(token2);
    }

    [Test]
    public async Task Handle_WithUserWithoutUsername_StillSucceeds()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", name: null)
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        await Assert.That(okResult.Value!.Token).IsNotEmpty();
    }

    [Test]
    public async Task Handle_ResponseStructure_IsCorrect()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var tokenFactory = CreateTokenFactory();

        // Act
        var result = GetToken.Handle(user, tokenFactory);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<TokenResponse>>();
        var okResult = (Ok<TokenResponse>)result;
        var response = okResult.Value!;

        // Verify response has required properties
        await Assert.That(response.Token).IsNotNull();
        await Assert.That(response.ExpiresAt).IsGreaterThan(DateTimeOffset.UtcNow);
    }
}
