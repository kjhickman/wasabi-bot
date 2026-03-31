using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WasabiBot.Api.Components.Pages;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Components;

public class TokenGeneratorComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    public TokenGeneratorComponentTests()
    {
        _context.Services.AddSingleton(ComponentTestHelpers.CreateTokenFactory());
    }

    [Test]
    public async Task Submit_WithValidUser_DisplaysTokenAndExpiry()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsApiUser("123456789", "testuser")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = RenderWithAuthentication<TokenGenerator>(authState);

        cut.Find("form").Submit();

        await Assert.That(cut.Find("#token-output").TextContent.Trim()).IsNotEmpty();
        await Assert.That(cut.Markup).Contains("Expires at:");
        await Assert.That(cut.FindAll(".error-message").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Submit_WithMissingUserId_ShowsErrorAndNoToken()
    {
        var user = ClaimsPrincipalBuilder.Create()
            .AsUnauthenticatedUser()
            .WithName("testuser")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = RenderWithAuthentication<TokenGenerator>(authState);

        cut.Find("form").Submit();

        await Assert.That(cut.Find(".error-message").TextContent).Contains("Unable to generate token");
        await Assert.That(cut.FindAll("#token-output").Count).IsEqualTo(0);
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
