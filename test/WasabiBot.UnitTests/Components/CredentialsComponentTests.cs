using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WasabiBot.Api.Components.Pages;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Components;

public class CredentialsComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    public CredentialsComponentTests()
    {
        var credentialService = Substitute.For<IApiCredentialService>();
        credentialService.ListAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _context.Services.AddSingleton(credentialService);
    }

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
        var credentialService = Substitute.For<IApiCredentialService>();
        credentialService.ListAsync(123456789, Arg.Any<CancellationToken>())
            .Returns([
                new ApiCredentialSummary(1, "My api creds", "wb_client_1", DateTimeOffset.UtcNow, null, null)
            ]);
        _context.Services.AddSingleton<IApiCredentialService>(credentialService);

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .Build();
        var authState = new AuthenticationState(user);
        var navigationManager = _context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("/creds?modal=create");

        var cut = _context.RenderWithAuthentication<Credentials>(authState);

        await Assert.That(cut.Find("#credentials-create-open-button").TextContent.Trim()).IsEqualTo("Create");
        await Assert.That(cut.Find("#credentials-create-open-button").GetAttribute("href")).IsEqualTo("/creds?modal=create");
        await Assert.That(cut.Find("#credential-name-input").GetAttribute("maxlength")).IsEqualTo("100");
        await Assert.That(cut.Find("#credential-name-input").GetAttribute("placeholder")).IsEqualTo("My api creds");
        await Assert.That(cut.FindAll("#credentials-secret-modal").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#credentials-table").TextContent).Contains("Name");
        await Assert.That(cut.Find("#credentials-table").TextContent).Contains("Client ID");
        await Assert.That(cut.Find("#credentials-table").TextContent).Contains("Last Used");
        await Assert.That(cut.Markup).Contains("wb_client_1");
        await Assert.That(cut.Markup).Contains("Create a credential");
        await Assert.That(cut.Markup).Contains("Your API Credentials");
        await Assert.That(cut.FindAll("#credentials-refresh-button").Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
