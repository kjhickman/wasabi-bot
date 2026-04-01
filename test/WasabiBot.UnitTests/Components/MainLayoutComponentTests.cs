using Bunit;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;
using WasabiBot.Api.Components.Layout;

namespace WasabiBot.UnitTests.Components;

public class MainLayoutComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_UnauthenticatedUser_ShowsBrandAndSignIn()
    {
        _context.AddAuthorization();

        var cut = RenderLayout();

        await Assert.That(cut.Markup).Contains("Wasabi Bot");
        await Assert.That(cut.FindAll("#logout-button").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("#header-user-name").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsIdentitySummaryAndLogout()
    {
        var authContext = _context.AddAuthorization();
        authContext.SetAuthorized("kyle");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "123456789"),
            new Claim("urn:discord:user:global_name", "Kyle"),
            new Claim("urn:discord:avatar:hash", "avatarhash"));

        var cut = RenderLayout();
        cut.WaitForElement("#header-user-name");

        await Assert.That(cut.Find("#header-user-name").TextContent.Trim()).IsEqualTo("Kyle");
        await Assert.That(cut.Markup).DoesNotContain("Hello,");
        await Assert.That(cut.Find("#header-user-avatar").GetAttribute("src")).Contains("cdn.discordapp.com/avatars/123456789/avatarhash.png?size=128");
        await Assert.That(cut.Find("#account-menu-button").GetAttribute("aria-controls")).IsEqualTo("account-menu-panel");
        await Assert.That(cut.Find("#account-menu-button").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(cut.Find("#account-menu-button").GetAttribute("popovertarget")).IsEqualTo("account-menu-panel");
        await Assert.That(cut.Find("#theme-select").GetAttribute("data-theme-select")).IsEmpty();
        await Assert.That(cut.FindAll("#theme-select option").Count).IsEqualTo(3);
        await Assert.That(cut.Find("#logout-button").TextContent.Trim()).IsEqualTo("Log out");
    }

    [Test]
    public async Task Render_AuthenticatedUserWithoutCustomAvatar_ShowsDefaultDiscordAvatar()
    {
        var authContext = _context.AddAuthorization();
        authContext.SetAuthorized("kyle");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "123456789"),
            new Claim("urn:discord:user:global_name", "Kyle"),
            new Claim("urn:discord:user:discriminator", "1234"));

        var cut = RenderLayout();
        cut.WaitForElement("#header-user-name");

        await Assert.That(cut.Find("#header-user-avatar").GetAttribute("src")).Contains("cdn.discordapp.com/embed/avatars/4.png");
        await Assert.That(cut.FindAll("#header-user-avatar-fallback").Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private IRenderedComponent<MainLayout> RenderLayout()
    {
        RenderFragment body = builder => builder.AddMarkupContent(0, "<p>Body</p>");
        return _context.Render<MainLayout>(parameters => parameters.Add(component => component.Body, body));
    }
}
