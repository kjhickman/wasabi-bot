using WasabiBot.Api.Core.Extensions;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Core.Extensions;

public class ClaimsExtensionsTests
{
    [Test]
    public async Task DisplayName_WithWhitespaceGlobalName_FallsBackToUsername()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .WithDiscordGlobalName("   ")
            .WithDiscordUsername("testuser")
            .Build();

        await Assert.That(user.DisplayName).IsEqualTo("testuser");
    }

    [Test]
    public async Task DisplayName_WithWhitespaceClaims_ReturnsDefaultUser()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .WithDiscordGlobalName(" ")
            .WithDiscordGlobalNameAlt(" ")
            .WithDiscordUsername(" ")
            .WithName(" ")
            .Build();

        await Assert.That(user.DisplayName).IsEqualTo("User");
    }

    [Test]
    public async Task DiscordAvatarUrl_WithoutAvatarHash_UsesDefaultDiscordAvatar()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .WithDiscordDiscriminator("1234")
            .Build();

        await Assert.That(user.DiscordAvatarUrl).IsEqualTo("https://cdn.discordapp.com/embed/avatars/4.png");
    }

    [Test]
    public async Task DiscordAvatarUrl_WithWhitespaceUserId_ReturnsNull()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("   ")
            .WithDiscordAvatar("avatarhash")
            .Build();

        await Assert.That(user.DiscordAvatarUrl).IsNull();
    }
}
