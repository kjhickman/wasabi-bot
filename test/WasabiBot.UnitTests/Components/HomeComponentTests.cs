using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WasabiBot.Api.Components.Pages;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Components;

public class HomeComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_UnauthenticatedUser_ShowsLoginLink()
    {
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var cut = RenderWithAuthentication<Home>(authState);

        await Assert.That(cut.Find("#login-link").GetAttribute("href")).IsEqualTo("/auth/login-discord");
        await Assert.That(cut.Markup).Contains("Sign in with Discord");
        await Assert.That(cut.FindAll("#authenticated").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsGreetingAndTokenGenerator()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);
        _context.Services.AddSingleton(ComponentTestHelpers.CreateTokenFactory());

        var cut = RenderWithAuthentication<Home>(authState);

        await Assert.That(cut.Find("#user-greeting").TextContent.Trim()).IsEqualTo("Kyle");
        await Assert.That(cut.Markup).Contains("Token Generator");
        await Assert.That(cut.FindAll("#login-link").Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private IRenderedComponent<TComponent> RenderWithAuthentication<TComponent>(AuthenticationState authState)
        where TComponent : IComponent
    {
        var wrapper = _context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(parameter => parameter.Value, Task.FromResult(authState))
            .AddChildContent<TComponent>());

        return wrapper.FindComponent<TComponent>();
    }
}
