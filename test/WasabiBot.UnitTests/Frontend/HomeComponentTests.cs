using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WasabiBot.Api.Frontend.Pages;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Frontend;

public class HomeComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_UnauthenticatedUser_ShowsLoginLink()
    {
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(false));
        var cut = _context.RenderWithAuthentication<Home>(authState);

        await Assert.That(cut.Find("#login-link").GetAttribute("href")).IsEqualTo("/login-discord");
        await Assert.That(cut.Markup).Contains("Sign in to Wasabi Bot");
        await Assert.That(cut.Markup).DoesNotContain("shared Discord server");
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsGreetingOnly()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));

        var cut = _context.RenderWithAuthentication<Home>(authState);

        await Assert.That(cut.Find("#user-greeting").TextContent.Trim()).IsEqualTo("Kyle");
        await Assert.That(cut.Find("#authenticated-heading").TextContent.Trim()).IsEqualTo("Hello, Kyle");
        await Assert.That(cut.Markup).DoesNotContain("Get API Access");
        await Assert.That(cut.Markup).DoesNotContain("Music");
        await Assert.That(cut.Markup).DoesNotContain("Stats");
        await Assert.That(cut.FindAll("#login-link").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUserWithoutGuildAccess_ShowsRestrictedMessage()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(false));
        var cut = _context.RenderWithAuthentication<Home>(authState);

        await Assert.That(cut.Markup).Contains("You must be in a server with Wasabi Bot to access this site.");
        await Assert.That(cut.FindAll("#authenticated").Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed class TestAuthorizationService(bool authorize) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(authorize ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            return Task.FromResult(authorize ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
