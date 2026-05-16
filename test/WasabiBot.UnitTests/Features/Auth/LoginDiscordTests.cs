using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Api.Features.Auth;

namespace WasabiBot.UnitTests.Features.Auth;

public class LoginDiscordTests
{
    [Test]
    public async Task Handle_SetsPersistentAuthenticationProperties()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = LoginDiscord.Handle(context, null);

        // Assert
        await Assert.That(result).IsTypeOf<ChallengeHttpResult>();
        var challenge = (ChallengeHttpResult)result;
        await Assert.That(challenge.AuthenticationSchemes).Contains(DiscordAuthenticationDefaults.AuthenticationScheme);
        await Assert.That(challenge.Properties).IsNotNull();
        await Assert.That(challenge.Properties!.IsPersistent).IsTrue();
        await Assert.That(challenge.Properties.RedirectUri).IsEqualTo("/");
    }
}
