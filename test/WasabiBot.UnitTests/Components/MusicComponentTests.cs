using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WasabiBot.Api.Components.Features.Music;
using WasabiBot.Api.Features.Music;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Components;

public class MusicComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_UnauthenticatedUser_ShowsLoginPrompt()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(false));
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardService>());
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var cut = _context.RenderWithAuthentication<Music>(authState);

        await Assert.That(cut.Find("#music-login-link").GetAttribute("href")).IsEqualTo("/login-discord?returnUrl=%2Fmusic");
        await Assert.That(cut.Markup).Contains("Sign in to view live music");
        await Assert.That(cut.FindAll("#music-page").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUserWithoutSharedVoiceChannel_ShowsJoinPrompt()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns((ActiveMusicSession?)null);
        _context.Services.AddSingleton(dashboardService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = _context.RenderWithAuthentication<Music>(authState);

        await Assert.That(cut.Markup).Contains("Join a voice channel with Wasabi Bot");
        await Assert.That(cut.Find("#music-page-heading").TextContent.Trim()).IsEqualTo("Music Dashboard");
    }

    [Test]
    public async Task Render_AuthenticatedUserWithActiveSession_ShowsNowPlayingAndQueue()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new ActiveMusicSession(
                new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
                new MusicTrackSnapshot("Current Song", "Current Artist", "03:00", IsLive: false, IsRadio: false),
                [new MusicQueueItemSnapshot(1, new MusicTrackSnapshot("Next Song", "Next Artist", "01:35", IsLive: false, IsRadio: false))]));
        _context.Services.AddSingleton(dashboardService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = _context.RenderWithAuthentication<Music>(authState);

        await Assert.That(cut.Find("#music-guild-name").TextContent.Trim()).IsEqualTo("Wasabi HQ");
        await Assert.That(cut.Find("#music-channel-name").TextContent.Trim()).IsEqualTo("#music-room");
        await Assert.That(cut.Find("#music-now-playing-track").TextContent).Contains("Current Song");
        await Assert.That(cut.Find("#music-queue-list").TextContent).Contains("Next Song");
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
