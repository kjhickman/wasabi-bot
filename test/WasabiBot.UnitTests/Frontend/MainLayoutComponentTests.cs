using Bunit;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;
using WasabiBot.Api.Frontend.Layout;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Frontend;

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
        await Assert.That(cut.FindAll("#nav-link-creds").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("#side-nav").Count).IsEqualTo(0);
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
        cut.WaitForElement("#account-menu-button");

        await Assert.That(cut.FindAll("#header-user-name").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("Hello,");
        await Assert.That(cut.Find("#header-user-avatar").GetAttribute("src")).Contains("cdn.discordapp.com/avatars/123456789/avatarhash.png?size=128");
        await Assert.That(cut.Find("#header-user-avatar").GetAttribute("alt")).IsEqualTo("Kyle");
        await Assert.That(cut.Find("#account-menu-panel form").GetAttribute("action")).IsEqualTo("/logout");
        await Assert.That(cut.Find("#logout-button").TextContent.Trim()).IsEqualTo("Log out");
        await Assert.That(cut.Find("#account-menu-panel").TextContent).DoesNotContain("Kyle");
        await Assert.That(cut.Find("#nav-search-form").GetAttribute("action")).IsEqualTo("/music/search");
        await Assert.That(cut.Find("#nav-search-form").GetAttribute("method")).IsEqualTo("get");
        await Assert.That(cut.Find("#nav-search-query").GetAttribute("name")).IsEqualTo("query");
        await Assert.That(cut.FindAll("#nav-search-form button").Count).IsEqualTo(0);
        var apiAccessLink = cut.Find("#account-menu-panel #nav-link-creds");
        await Assert.That(apiAccessLink.GetAttribute("href")).IsEqualTo("/creds");
        await Assert.That(apiAccessLink.HasAttribute("data-wasabi-close-dropdown")).IsTrue();
        await Assert.That(cut.FindAll("#nav-link-music").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#side-nav").GetAttribute("aria-label")).IsEqualTo("Primary sections");
        await Assert.That(cut.Find("#music-tab-live").GetAttribute("href")).IsEqualTo("/");
        await Assert.That(cut.FindAll("#music-tab-search").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#music-tab-favorites").GetAttribute("href")).IsEqualTo("/music/library");
        await Assert.That(cut.Find("#music-tab-top-played").GetAttribute("href")).IsEqualTo("/music/stats");
        await Assert.That(cut.Find("#side-nav-api-access").GetAttribute("href")).IsEqualTo("/creds");
        await Assert.That(cut.FindAll("#side-nav svg").Count).IsEqualTo(4);
        await Assert.That(cut.Find("#account-menu-button").TagName).IsEqualTo("BUTTON");
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
        cut.WaitForElement("#account-menu-button");

        await Assert.That(cut.Find("#header-user-avatar").GetAttribute("src")).Contains("cdn.discordapp.com/embed/avatars/4.png");
        await Assert.That(cut.FindAll("#header-user-avatar-fallback").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUserWithoutAvatarData_ShowsInitialFallbackAvatar()
    {
        var authContext = _context.AddAuthorization();
        authContext.SetAuthorized("kyle");

        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("not-a-snowflake")
            .WithDiscordGlobalName("Kyle")
            .Build();

        authContext.SetClaims(user.Claims.ToArray());

        var cut = RenderLayout();
        cut.WaitForElement("#account-menu-button");

        await Assert.That(cut.FindAll("#header-user-avatar").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#header-user-avatar-fallback").TextContent.Trim()).IsEqualTo("K");
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
