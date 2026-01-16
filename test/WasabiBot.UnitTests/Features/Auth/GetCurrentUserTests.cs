using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Api.Features.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Auth;

public class GetCurrentUserTests
{

    [Test]
    public async Task Handle_WithValidUserAndAllClaims_ReturnsOkWithCompleteResponse()
    {
        // Arrange
        var userId = "123456789";
        var username = "testuser";
        var globalName = "Test User";
        var discriminator = "0001";
        var avatarHash = "abc123def456";
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser(userId, username)
            .WithDiscordGlobalName(globalName)
            .WithDiscordDiscriminator(discriminator)
            .WithDiscordAvatar(avatarHash)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        var response = okResult.Value;

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.UserId).IsEqualTo(userId);
        await Assert.That(response.Username).IsEqualTo(username);
        await Assert.That(response.GlobalName).IsEqualTo(globalName);
        await Assert.That(response.Discriminator).IsEqualTo(discriminator);
        await Assert.That(response.AvatarUrl).IsEqualTo($"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=128");
    }

    [Test]
    public async Task Handle_WithMinimalClaims_ReturnsOkWithPartialResponse()
    {
        // Arrange
        var userId = "987654321";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId(userId)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        var response = okResult.Value;

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.UserId).IsEqualTo(userId);
        await Assert.That(response.Username).IsNull();
        await Assert.That(response.GlobalName).IsNull();
        await Assert.That(response.Discriminator).IsNull();
        await Assert.That(response.AvatarUrl).IsNull();
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsUnauthenticatedUser()
            .WithDiscordUsername("testuser")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }

    [Test]
    public async Task Handle_WithEmptyUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("")
            .WithDiscordUsername("testuser")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }

    [Test]
    public async Task Handle_WithWhitespaceUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("   ")
            .WithDiscordUsername("testuser")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }

    [Test]
    public async Task Handle_WithAvatarHash_ConstructsCorrectAvatarUrl()
    {
        // Arrange
        var userId = "555555555";
        var avatarHash = "abc123def456789";
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser(userId, "testuser")
            .WithDiscordGlobalName("Test User")
            .WithDiscordDiscriminator("0001")
            .WithDiscordAvatar(avatarHash)
            .Build();
        var expectedUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=128";

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.AvatarUrl).IsEqualTo(expectedUrl);
    }

    [Test]
    public async Task Handle_WithoutAvatarHash_ReturnsNullAvatarUrl()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.AvatarUrl).IsNull();
    }

    [Test]
    public async Task Handle_WithEmptyAvatarHash_ReturnsNullAvatarUrl()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .WithDiscordAvatar("")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.AvatarUrl).IsNull();
    }

    [Test]
    public async Task Handle_WithWhitespaceAvatarHash_ReturnsNullAvatarUrl()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .WithDiscordAvatar("   ")
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.AvatarUrl).IsNull();
    }

    [Test]
    public async Task Handle_WithDiscordUsernameClaim_ExtractsUsername()
    {
        // Arrange
        var username = "discord_username";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordUsername(username)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.Username).IsEqualTo(username);
    }

    [Test]
    public async Task Handle_WithNameClaim_ExtractsUsername()
    {
        // Arrange
        var username = "standard_name";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithName(username)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.Username).IsEqualTo(username);
    }

    [Test]
    public async Task Handle_WithBothUsernameClaims_PrefersDiscordClaim()
    {
        // Arrange
        var discordUsername = "discord_username";
        var standardName = "standard_name";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordUsername(discordUsername)
            .WithName(standardName)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.Username).IsEqualTo(discordUsername);
    }

    [Test]
    public async Task Handle_WithGlobalNameClaim_ExtractsGlobalName()
    {
        // Arrange
        var globalName = "Global Display Name";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordGlobalName(globalName)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.GlobalName).IsEqualTo(globalName);
    }

    [Test]
    public async Task Handle_WithAlternativeGlobalNameClaim_ExtractsGlobalName()
    {
        // Arrange
        var globalName = "Alternative Global Name";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordGlobalNameAlt(globalName)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.GlobalName).IsEqualTo(globalName);
    }

    [Test]
    public async Task Handle_WithBothGlobalNameClaims_PrefersUnderscoreVersion()
    {
        // Arrange
        var globalNameUnderscore = "global_name_version";
        var globalNameNoUnderscore = "globalname_version";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordGlobalName(globalNameUnderscore)
            .WithDiscordGlobalNameAlt(globalNameNoUnderscore)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.GlobalName).IsEqualTo(globalNameUnderscore);
    }

    [Test]
    public async Task Handle_WithDiscriminator_ExtractsDiscriminator()
    {
        // Arrange
        var discriminator = "1234";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123")
            .WithDiscordDiscriminator(discriminator)
            .Build();

        // Act
        var result = GetCurrentUser.Handle(user);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<CurrentUserResponse>>();
        var okResult = (Ok<CurrentUserResponse>)result;
        await Assert.That(okResult.Value!.Discriminator).IsEqualTo(discriminator);
    }
}
