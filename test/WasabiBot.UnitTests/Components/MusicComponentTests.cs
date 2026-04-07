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
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardControlService>());
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardSearchService>());
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardQueueService>());
        _context.Services.AddSingleton(Substitute.For<IMusicFavoritesService>());
        _context.Services.AddSingleton(Substitute.For<IMusicGuildStatsService>());
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/music");

        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var cut = _context.RenderWithAuthentication<Music>(authState);

        await Assert.That(cut.Find("#music-login-link").GetAttribute("href")).IsEqualTo("/login-discord?returnUrl=/music");
        await Assert.That(cut.Markup).Contains("Sign in to control music");
        await Assert.That(cut.FindAll("#music-shell").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUserWithoutSharedVoiceChannel_ShowsJoinPrompt()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardControlService>());
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardSearchService>());
        _context.Services.AddSingleton(Substitute.For<IMusicDashboardQueueService>());
        _context.Services.AddSingleton(Substitute.For<IMusicFavoritesService>());
        _context.Services.AddSingleton(Substitute.For<IMusicGuildStatsService>());
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

        await Assert.That(cut.Markup).Contains("Waiting for a live room");
        await Assert.That(cut.FindAll("#music-page-heading").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUserWithActiveSession_ShowsNowPlayingAndQueue()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        var controlService = Substitute.For<IMusicDashboardControlService>();
        var searchService = Substitute.For<IMusicDashboardSearchService>();
        var queueService = Substitute.For<IMusicDashboardQueueService>();
        var favoritesService = Substitute.For<IMusicFavoritesService>();
        var guildStatsService = Substitute.For<IMusicGuildStatsService>();
        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new ActiveMusicSession(
                new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
                "Playing",
                new PlaybackProgressSnapshot(TimeSpan.FromMinutes(1.5), "01:30", 50),
                new MusicTrackSnapshot(
                    "Current Song",
                    "Current Artist",
                    "03:00",
                    TimeSpan.FromMinutes(3),
                    IsLive: false,
                    IsRadio: false,
                    ArtworkUrl: "https://cdn.example.com/current-song.jpg",
                    SourceUrl: "https://soundcloud.com/example/current-song",
                    SourceName: "scsearch"),
                [new MusicQueueItemSnapshot(1, new MusicTrackSnapshot(
                    "Next Song",
                    "Next Artist",
                    "01:35",
                    TimeSpan.FromSeconds(95),
                    IsLive: false,
                    IsRadio: false,
                    ArtworkUrl: null,
                    SourceUrl: "https://soundcloud.com/example/next-song",
                    SourceName: "scsearch"))]));
        _context.Services.AddSingleton(dashboardService);
        _context.Services.AddSingleton(controlService);
        _context.Services.AddSingleton(searchService);
        _context.Services.AddSingleton(queueService);
        favoritesService.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(new MusicFavoritesSnapshot([], []));
        _context.Services.AddSingleton(favoritesService);
        guildStatsService.GetTopTracksAsync(42, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _context.Services.AddSingleton(guildStatsService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = _context.RenderWithAuthentication<Music>(authState);

        await Assert.That(cut.Find("#music-now-playing-track").TextContent).Contains("Current Song");
        await Assert.That(cut.Find("#music-source-name").TextContent.Trim()).IsEqualTo("SoundCloud");
        await Assert.That(cut.Find("#music-source-link").GetAttribute("href")).IsEqualTo("https://soundcloud.com/example/current-song");
        await Assert.That(cut.Find("#music-artwork").GetAttribute("src")).IsEqualTo("https://cdn.example.com/current-song.jpg");
        await Assert.That(cut.Find("#music-progress-position").TextContent.Trim()).IsEqualTo("01:30");
        await Assert.That(cut.Find("#music-progress-duration").TextContent.Trim()).IsEqualTo("03:00");
        await Assert.That(cut.Find("#music-queue-list").TextContent).Contains("Next Song");
        await Assert.That(cut.Find("#music-skip").HasAttribute("disabled")).IsFalse();
        await Assert.That(cut.Find("#music-stop").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task Render_AuthenticatedUser_ClickingSkip_CallsSkipService()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        var controlService = Substitute.For<IMusicDashboardControlService>();
        var searchService = Substitute.For<IMusicDashboardSearchService>();
        var queueService = Substitute.For<IMusicDashboardQueueService>();
        var favoritesService = Substitute.For<IMusicFavoritesService>();
        var guildStatsService = Substitute.For<IMusicGuildStatsService>();

        var session = new ActiveMusicSession(
            new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
            "Playing",
            new PlaybackProgressSnapshot(TimeSpan.FromMinutes(1), "01:00", 25),
            new MusicTrackSnapshot(
                "Current Song",
                "Current Artist",
                "04:00",
                TimeSpan.FromMinutes(4),
                IsLive: false,
                IsRadio: false,
                ArtworkUrl: null,
                SourceUrl: null,
                SourceName: "scsearch"),
            []);

        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(session);
        controlService.SkipAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new MusicCommandResult("Skipped the current track."));

        _context.Services.AddSingleton(dashboardService);
        _context.Services.AddSingleton(controlService);
        _context.Services.AddSingleton(searchService);
        _context.Services.AddSingleton(queueService);
        favoritesService.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(new MusicFavoritesSnapshot([], []));
        _context.Services.AddSingleton(favoritesService);
        guildStatsService.GetTopTracksAsync(42, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _context.Services.AddSingleton(guildStatsService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();
        var authState = new AuthenticationState(user);

        var cut = _context.RenderWithAuthentication<Music>(authState);
        await cut.InvokeAsync(() => cut.Find("#music-skip").Click());

        await controlService.Received(1).SkipAsync(123456789, Arg.Any<CancellationToken>());
        await Assert.That(cut.FindAll("#music-action-message").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_AuthenticatedUser_Searching_ShowsSongAndRadioResults()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        var controlService = Substitute.For<IMusicDashboardControlService>();
        var searchService = Substitute.For<IMusicDashboardSearchService>();
        var queueService = Substitute.For<IMusicDashboardQueueService>();
        var favoritesService = Substitute.For<IMusicFavoritesService>();
        var guildStatsService = Substitute.For<IMusicGuildStatsService>();

        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new ActiveMusicSession(
                new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
                "Playing",
                null,
                new MusicTrackSnapshot("Current Song", "Artist", "03:00", TimeSpan.FromMinutes(3), false, false, null, null, "scsearch"),
                []));

        searchService.SearchAsync("radiohead", Arg.Any<CancellationToken>())
            .Returns(new MusicDashboardSearchResults(
                [new MusicDashboardSongSearchResult("Creep", "Radiohead", "03:58", null, "https://soundcloud.com/radiohead/creep", "scsearch", new Lavalink4NET.Tracks.LavalinkTrack { Identifier = "creep", Title = "Creep", Author = "Radiohead" })],
                [new MusicDashboardRadioSearchResult("Radiohead FM", "UK", "alternative", null, "https://example.com/radiohead", new WasabiBot.Api.Features.Radio.RadioBrowserStation { StationUuid = "station-1", Name = "Radiohead FM", UrlResolved = "https://stream.example.com/radiohead", LastCheckOk = 1 })],
                null));
        favoritesService.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(new MusicFavoritesSnapshot([], []));
        guildStatsService.GetTopTracksAsync(42, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        _context.Services.AddSingleton(dashboardService);
        _context.Services.AddSingleton(controlService);
        _context.Services.AddSingleton(searchService);
        _context.Services.AddSingleton(queueService);
        _context.Services.AddSingleton(favoritesService);
        _context.Services.AddSingleton(guildStatsService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();

        var cut = _context.RenderWithAuthentication<MusicShell>(new AuthenticationState(user), parameters => parameters
            .Add(x => x.ActivePage, MusicPageKind.Search));
        cut.Find("#music-search-query").Input("radiohead");
        await cut.InvokeAsync(() => cut.Find("#music-search-submit").Click());

        await Assert.That(cut.Find("#music-song-results").TextContent).Contains("Creep");
        await Assert.That(cut.Find("#music-radio-results").TextContent).Contains("Radiohead FM");
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsFavoriteSongsAndRadio()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        var controlService = Substitute.For<IMusicDashboardControlService>();
        var searchService = Substitute.For<IMusicDashboardSearchService>();
        var queueService = Substitute.For<IMusicDashboardQueueService>();
        var favoritesService = Substitute.For<IMusicFavoritesService>();
        var guildStatsService = Substitute.For<IMusicGuildStatsService>();

        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new ActiveMusicSession(
                new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
                "Playing",
                null,
                new MusicTrackSnapshot("Current Song", "Artist", "03:00", TimeSpan.FromMinutes(3), false, false, null, null, "scsearch"),
                []));
        favoritesService.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(new MusicFavoritesSnapshot(
            [new MusicFavoriteSummary(1, WasabiBot.Api.Persistence.Entities.MusicFavoriteKind.Song, "Creep", "Radiohead", "scsearch", "https://soundcloud.com/radiohead/creep", "", DateTimeOffset.UtcNow, new MusicFavoriteSongMetadata("creep", "scsearch", "https://soundcloud.com/radiohead/creep", "", "03:58"), null)],
            [new MusicFavoriteSummary(2, WasabiBot.Api.Persistence.Entities.MusicFavoriteKind.Radio, "Radiohead FM", "UK", "Radio Browser", "https://example.com/radiohead", "", DateTimeOffset.UtcNow, null, new MusicFavoriteRadioMetadata("station-1", "https://stream.example.com/radiohead", "https://example.com/radiohead", "", "UK", "alternative, rock"))]));

        _context.Services.AddSingleton(dashboardService);
        _context.Services.AddSingleton(controlService);
        _context.Services.AddSingleton(searchService);
        _context.Services.AddSingleton(queueService);
        _context.Services.AddSingleton(favoritesService);
        guildStatsService.GetTopTracksAsync(42, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _context.Services.AddSingleton(guildStatsService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();

        var cut = _context.RenderWithAuthentication<MusicShell>(new AuthenticationState(user), parameters => parameters
            .Add(x => x.ActivePage, MusicPageKind.Library));

        await Assert.That(cut.Find("#music-favorite-songs").TextContent).Contains("Creep");
        await Assert.That(cut.Find("#music-favorite-radio").TextContent).Contains("Radiohead FM");
    }

    [Test]
    public async Task Render_AuthenticatedUser_ShowsMostPlayedTracks()
    {
        _context.Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(true));
        var dashboardService = Substitute.For<IMusicDashboardService>();
        var controlService = Substitute.For<IMusicDashboardControlService>();
        var searchService = Substitute.For<IMusicDashboardSearchService>();
        var queueService = Substitute.For<IMusicDashboardQueueService>();
        var favoritesService = Substitute.For<IMusicFavoritesService>();
        var guildStatsService = Substitute.For<IMusicGuildStatsService>();

        dashboardService.GetActiveSessionAsync(123456789, Arg.Any<CancellationToken>())
            .Returns(new ActiveMusicSession(
                new SharedVoiceChannel(42, "Wasabi HQ", 99, "music-room"),
                "Playing",
                null,
                new MusicTrackSnapshot("Current Song", "Artist", "03:00", TimeSpan.FromMinutes(3), false, false, null, null, "scsearch"),
                []));
        favoritesService.ListAsync(123456789, Arg.Any<CancellationToken>()).Returns(new MusicFavoritesSnapshot([], []));
        guildStatsService.GetTopTracksAsync(42, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([
            new GuildTopTrackSummary("Creep", "Radiohead", "scsearch", "https://soundcloud.com/radiohead/creep", "", 5, DateTimeOffset.UtcNow)
        ]);

        _context.Services.AddSingleton(dashboardService);
        _context.Services.AddSingleton(controlService);
        _context.Services.AddSingleton(searchService);
        _context.Services.AddSingleton(queueService);
        _context.Services.AddSingleton(favoritesService);
        _context.Services.AddSingleton(guildStatsService);
        _context.Renderer.SetRendererInfo(new RendererInfo("Static", false));

        var user = ClaimsPrincipalBuilder.Create()
            .AsDiscordUser("123456789", "kyle")
            .WithDiscordGlobalName("Kyle")
            .Build();

        var cut = _context.RenderWithAuthentication<MusicShell>(new AuthenticationState(user), parameters => parameters
            .Add(x => x.ActivePage, MusicPageKind.Stats));

        await Assert.That(cut.Find("#music-most-played-list").TextContent).Contains("Creep");
        await Assert.That(cut.Find("#music-most-played-list").TextContent).Contains("5 play(s)");
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
