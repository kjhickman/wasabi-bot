using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Infrastructure.Auth;

internal sealed class DiscordGuildRequirementHandler(
    IDiscordGuildAuthorizationClient authorizationClient,
    HybridCache cache,
    ILogger<DiscordGuildRequirementHandler> logger,
    Tracer tracer) : AuthorizationHandler<DiscordGuildRequirement>
{
    private const string BotGuildsCacheKey = "discord:bot-guilds";

    private static readonly HybridCacheEntryOptions BotGuildsCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private static readonly HybridCacheEntryOptions PositiveMembershipCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    private static readonly HybridCacheEntryOptions NegativeMembershipCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60)
    };

    private static readonly HybridCacheEntryOptions CacheReadOnlyOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
    };

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DiscordGuildRequirement requirement)
    {
        using var span = tracer.StartActiveSpan("auth.discord-guild.authorize");

        if (context.User.Identity is not { IsAuthenticated: true })
        {
            span.SetAttribute("auth.authenticated", false);
            return;
        }

        span.SetAttribute("auth.authenticated", true);

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null || !ulong.TryParse(userIdClaim.Value, out var userId))
        {
            span.SetAttribute("auth.user_id.valid", false);
            logger.LogWarning("Discord user id claim missing or invalid.");
            return;
        }

        span.SetAttribute("auth.user_id.valid", true);
        span.SetAttribute("auth.user_id", userId.ToString());

        var cancellationToken = context.GetCancellationToken();
        var guildIds = await cache.GetOrCreateAsync(
            BotGuildsCacheKey,
            async cancel => (await authorizationClient.GetBotGuildIdsAsync(cancel)).ToArray(),
            BotGuildsCacheOptions,
            cancellationToken: cancellationToken);

        span.SetAttribute("auth.guild_count", guildIds.Length);

        if (guildIds.Length == 0)
        {
            logger.LogWarning("Bot guild list is empty; denying access.");
            return;
        }

        foreach (var guildId in guildIds)
        {
            if (await IsUserInGuildAsync(guildId, userId, cancellationToken))
            {
                span.SetAttribute("auth.authorized", true);
                span.SetAttribute("auth.authorized_guild_id", guildId.ToString());
                context.Succeed(requirement);
                return;
            }
        }

        span.SetAttribute("auth.authorized", false);
    }

    private async Task<bool> IsUserInGuildAsync(ulong guildId, ulong userId, CancellationToken cancellationToken)
    {
        using var span = tracer.StartActiveSpan("auth.discord-guild.membership-check");
        span.SetAttribute("auth.guild_id", guildId.ToString());
        span.SetAttribute("auth.user_id", userId.ToString());

        var positiveKey = $"discord:user-in-guild:{guildId}:{userId}:positive";
        if (await IsCachedAsync(positiveKey, cancellationToken))
        {
            span.SetAttribute("auth.membership_cache_hit", true);
            span.SetAttribute("auth.membership_cache_result", "positive");
            return true;
        }

        var negativeKey = $"discord:user-in-guild:{guildId}:{userId}:negative";
        if (await IsCachedAsync(negativeKey, cancellationToken))
        {
            span.SetAttribute("auth.membership_cache_hit", true);
            span.SetAttribute("auth.membership_cache_result", "negative");
            return false;
        }

        span.SetAttribute("auth.membership_cache_hit", false);

        var isMember = await authorizationClient.IsUserInGuildAsync(guildId, userId, cancellationToken);
        span.SetAttribute("auth.membership_result", isMember);
        if (isMember)
        {
            await cache.SetAsync(positiveKey, true, PositiveMembershipCacheOptions, cancellationToken: cancellationToken);
            await cache.RemoveAsync(negativeKey, cancellationToken);
            return true;
        }

        await cache.SetAsync(negativeKey, true, NegativeMembershipCacheOptions, cancellationToken: cancellationToken);
        await cache.RemoveAsync(positiveKey, cancellationToken);
        return false;
    }

    private async Task<bool> IsCachedAsync(string key, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            key,
            _ => ValueTask.FromResult(false),
            CacheReadOnlyOptions,
            cancellationToken: cancellationToken);
    }
}

file static class AuthorizationHandlerContextExtensions
{
    public static CancellationToken GetCancellationToken(this AuthorizationHandlerContext context)
    {
        return (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
    }
}
