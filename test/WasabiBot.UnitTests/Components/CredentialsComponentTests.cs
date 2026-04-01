using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using WasabiBot.Api.Components.Pages;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Components;

public class CredentialsComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_UnauthenticatedUser_ShowsLoginPrompt()
    {
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var cut = _context.RenderWithAuthentication<Credentials>(authState);

        await Assert.That(cut.Find("#credentials-login-link").GetAttribute("href")).IsEqualTo("/auth/login-discord");
        await Assert.That(cut.Markup).Contains("Sign in to manage API credentials");
        await Assert.That(cut.FindAll("#credentials-page").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsCredentialsManagementShell()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = _context.RenderWithAuthentication<Credentials>(authState);

        await Assert.That(cut.Find("#credentials-page").GetAttribute("data-list-endpoint")).IsEqualTo("/api/v1/creds");
        await Assert.That(cut.Find("#credential-name-input").GetAttribute("maxlength")).IsEqualTo("100");
        await Assert.That(cut.Find("#credentials-secret-panel").HasAttribute("hidden")).IsTrue();
        await Assert.That(cut.Markup).Contains("Create a credential");
        await Assert.That(cut.Markup).Contains("Your credentials");
        await Assert.That(cut.Markup).Contains("/oauth/token");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
