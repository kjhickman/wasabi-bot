using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTelemetry.Trace;
using System.Security.Claims;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Infrastructure.Auth;

public class DiscordGuildRequirementHandlerTests
{
    [Test]
    public async Task HandleAsync_ShouldCacheGuildListAndMembershipChecksAcrossRequests()
    {
        var authorizationClient = Substitute.For<IDiscordGuildAuthorizationClient>();
        authorizationClient.GetBotGuildIdsAsync(Arg.Any<CancellationToken>()).Returns([1UL, 2UL]);
        authorizationClient.IsUserInGuildAsync(1UL, 42UL, Arg.Any<CancellationToken>()).Returns(false);
        authorizationClient.IsUserInGuildAsync(2UL, 42UL, Arg.Any<CancellationToken>()).Returns(true);

        var handler = CreateHandler(authorizationClient);
        var user = ClaimsPrincipalBuilder.Create().AsDiscordUser("42", "kyle").Build();

        var firstContext = CreateContext(user);
        await handler.HandleAsync(firstContext);

        var secondContext = CreateContext(user);
        await handler.HandleAsync(secondContext);

        await Assert.That(firstContext.HasSucceeded).IsTrue();
        await Assert.That(secondContext.HasSucceeded).IsTrue();
        await authorizationClient.Received(1).GetBotGuildIdsAsync(Arg.Any<CancellationToken>());
        await authorizationClient.Received(1).IsUserInGuildAsync(1UL, 42UL, Arg.Any<CancellationToken>());
        await authorizationClient.Received(1).IsUserInGuildAsync(2UL, 42UL, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_ShouldCacheNegativeMembershipChecksAcrossRequests()
    {
        var authorizationClient = Substitute.For<IDiscordGuildAuthorizationClient>();
        authorizationClient.GetBotGuildIdsAsync(Arg.Any<CancellationToken>()).Returns([1UL, 2UL]);
        authorizationClient.IsUserInGuildAsync(1UL, 42UL, Arg.Any<CancellationToken>()).Returns(false);
        authorizationClient.IsUserInGuildAsync(2UL, 42UL, Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler(authorizationClient);
        var user = ClaimsPrincipalBuilder.Create().AsDiscordUser("42", "kyle").Build();

        var firstContext = CreateContext(user);
        await handler.HandleAsync(firstContext);

        var secondContext = CreateContext(user);
        await handler.HandleAsync(secondContext);

        await Assert.That(firstContext.HasSucceeded).IsFalse();
        await Assert.That(secondContext.HasSucceeded).IsFalse();
        await authorizationClient.Received(1).GetBotGuildIdsAsync(Arg.Any<CancellationToken>());
        await authorizationClient.Received(1).IsUserInGuildAsync(1UL, 42UL, Arg.Any<CancellationToken>());
        await authorizationClient.Received(1).IsUserInGuildAsync(2UL, 42UL, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_WithInvalidDiscordUserId_ShouldSkipDiscordLookups()
    {
        var authorizationClient = Substitute.For<IDiscordGuildAuthorizationClient>();
        var handler = CreateHandler(authorizationClient);
        var user = ClaimsPrincipalBuilder.Create().WithUserId("not-a-number").Build();

        var context = CreateContext(user);
        await handler.HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
        await authorizationClient.DidNotReceive().GetBotGuildIdsAsync(Arg.Any<CancellationToken>());
        await authorizationClient.DidNotReceive().IsUserInGuildAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<CancellationToken>());
    }

    private static DiscordGuildRequirementHandler CreateHandler(IDiscordGuildAuthorizationClient authorizationClient)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<HybridCache>();

        return new DiscordGuildRequirementHandler(
            authorizationClient,
            cache,
            Substitute.For<ILogger<DiscordGuildRequirementHandler>>(),
            TracerProvider.Default.GetTracer("discord-guild-auth-tests"));
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user)
    {
        return new AuthorizationHandlerContext(
            [new DiscordGuildRequirement()],
            user,
            new DefaultHttpContext());
    }
}
