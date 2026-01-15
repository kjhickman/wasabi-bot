using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.UnitTests.Infrastructure.Auth;

public class ApiTokenFactoryTests
{
    private const string TestUserId = "test-user-id";
    private const string TestUsername = "test-username";
    private const string DiscordUsernameClaimType = "urn:discord:user:username";
    
    private static SymmetricSecurityKey CreateTestSigningKey()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        return new SymmetricSecurityKey(key);
    }

    private static ClaimsPrincipal CreateTestUser(string userId, string? username = null, bool useDiscordClaim = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        if (username is not null)
        {
            var claimType = useDiscordClaim ? DiscordUsernameClaimType : ClaimTypes.Name;
            claims.Add(new Claim(claimType, username));
        }

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    [Test]
    public async Task Constructor_WithValidOptions_SetsProperties()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var lifetime = TimeSpan.FromHours(1);
        var options = new ApiTokenOptions(signingKey, lifetime);

        // Act
        var factory = new ApiTokenFactory(options);

        // Assert
        await Assert.That(factory.Lifetime).IsEqualTo(lifetime);
    }

    [Test]
    public async Task TryCreateToken_WithValidUser_ReturnsTrue()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        var result = factory.TryCreateToken(user, out var token);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(token).IsNotEmpty();
    }

    [Test]
    public async Task TryCreateToken_WithValidUser_CreatesValidJwt()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        var result = factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(jwtToken).IsNotNull();
        await Assert.That(jwtToken!.Claims.Count).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task TryCreateToken_WithValidUser_IncludesUserIdClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var userIdClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        await Assert.That(userIdClaim).IsNotNull();
        await Assert.That(userIdClaim!.Value).IsEqualTo(TestUserId);
    }

    [Test]
    public async Task TryCreateToken_WithValidUser_IncludesSubjectClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var subClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        await Assert.That(subClaim).IsNotNull();
        await Assert.That(subClaim!.Value).IsEqualTo(TestUserId);
    }

    [Test]
    public async Task TryCreateToken_WithValidUser_IncludesJtiClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var jtiClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        await Assert.That(jtiClaim).IsNotNull();
        await Assert.That(jtiClaim!.Value).IsNotEmpty();
        // Verify that the Jti is a valid GUID
        await Assert.That(Guid.TryParse(jtiClaim.Value, out _)).IsTrue();
    }

    [Test]
    public async Task TryCreateToken_WithUsernameInNameIdentifier_IncludesNameClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var nameClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        await Assert.That(nameClaim).IsNotNull();
        await Assert.That(nameClaim!.Value).IsEqualTo(TestUsername);
    }

    [Test]
    public async Task TryCreateToken_WithDiscordUsernameClaim_IncludesNameClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername, useDiscordClaim: true);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var nameClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        await Assert.That(nameClaim).IsNotNull();
        await Assert.That(nameClaim!.Value).IsEqualTo(TestUsername);
    }

    [Test]
    public async Task TryCreateToken_WithoutUsername_DoesNotIncludeNameClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var nameClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        await Assert.That(nameClaim).IsNull();
    }

    [Test]
    public async Task TryCreateToken_WithNullUser_ThrowsException()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);

        // Act & Assert
        await Assert.That(() => factory.TryCreateToken(null!, out _)).Throws<NullReferenceException>();
    }

    [Test]
    public async Task TryCreateToken_WithoutUserIdClaim_ReturnsFalse()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var identity = new ClaimsIdentity([], "test");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = factory.TryCreateToken(user, out var token);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(token).IsEmpty();
    }

    [Test]
    public async Task TryCreateToken_WithEmptyUserIdClaim_ReturnsFalse()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, string.Empty) };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = factory.TryCreateToken(user, out var token);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(token).IsEmpty();
    }

    [Test]
    public async Task TryCreateToken_WithWhitespaceUserIdClaim_ReturnsFalse()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "   ") };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = factory.TryCreateToken(user, out var token);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(token).IsEmpty();
    }

    [Test]
    public async Task TryCreateToken_CreatedTokenHasCorrectExpiration()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var lifetime = TimeSpan.FromHours(2);
        var options = new ApiTokenOptions(signingKey, lifetime);
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);
        var beforeCreation = DateTime.UtcNow;

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
        var afterCreation = DateTime.UtcNow;

        // Assert - allow up to 2 seconds of tolerance for test execution
        var expectedMinExpiration = beforeCreation.Add(lifetime).AddSeconds(-2);
        var expectedMaxExpiration = afterCreation.Add(lifetime).AddSeconds(2);
        
        await Assert.That(jwtToken!.ValidTo).IsGreaterThanOrEqualTo(expectedMinExpiration);
        await Assert.That(jwtToken.ValidTo).IsLessThanOrEqualTo(expectedMaxExpiration);
    }

    [Test]
    public async Task TryCreateToken_CreatedTokenHasValidNotBeforeTime()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);
        var beforeCreation = DateTime.UtcNow;

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
        var afterCreation = DateTime.UtcNow;

        // Assert
        await Assert.That(jwtToken!.ValidFrom).IsGreaterThanOrEqualTo(beforeCreation.AddSeconds(-1));
        await Assert.That(jwtToken.ValidFrom).IsLessThanOrEqualTo(afterCreation.AddSeconds(1));
    }

    [Test]
    public async Task TryCreateToken_MultipleTokensHaveDifferentJti()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory.TryCreateToken(user, out var token1);
        factory.TryCreateToken(user, out var token2);
        
        var handler = new JwtSecurityTokenHandler();
        var jwtToken1 = handler.ReadToken(token1) as JwtSecurityToken;
        var jwtToken2 = handler.ReadToken(token2) as JwtSecurityToken;

        var jti1 = jwtToken1!.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwtToken2!.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        // Assert
        await Assert.That(jti1).IsNotEqualTo(jti2);
    }

    [Test]
    public async Task TryCreateToken_WithDifferentSigningKeys_ProducesDifferentTokens()
    {
        // Arrange
        var signingKey1 = CreateTestSigningKey();
        var signingKey2 = CreateTestSigningKey();
        var options1 = new ApiTokenOptions(signingKey1, TimeSpan.FromHours(1));
        var options2 = new ApiTokenOptions(signingKey2, TimeSpan.FromHours(1));
        var factory1 = new ApiTokenFactory(options1);
        var factory2 = new ApiTokenFactory(options2);
        var user = CreateTestUser(TestUserId, TestUsername);

        // Act
        factory1.TryCreateToken(user, out var token1);
        factory2.TryCreateToken(user, out var token2);

        // Assert
        await Assert.That(token1).IsNotEqualTo(token2);
    }

    [Test]
    public async Task TryCreateToken_PreferNameClaimOverDiscordClaim()
    {
        // Arrange
        var signingKey = CreateTestSigningKey();
        var options = new ApiTokenOptions(signingKey, TimeSpan.FromHours(1));
        var factory = new ApiTokenFactory(options);
        
        // Create user with both Name and Discord username claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, TestUserId),
            new(ClaimTypes.Name, "preferred-username"),
            new(DiscordUsernameClaimType, "discord-username")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);

        // Act
        factory.TryCreateToken(user, out var token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        var nameClaim = jwtToken!.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        await Assert.That(nameClaim).IsNotNull();
        await Assert.That(nameClaim!.Value).IsEqualTo("preferred-username");
    }
}
