using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NetCord.Rest;

namespace WasabiBot.Api.Infrastructure.Auth;

internal sealed class DiscordGuildRequirementHandler : AuthorizationHandler<DiscordGuildRequirement>
{
    private readonly RestClient _restClient;
    private readonly ILogger<DiscordGuildRequirementHandler> _logger;

    public DiscordGuildRequirementHandler(RestClient restClient, ILogger<DiscordGuildRequirementHandler> logger)
    {
        _restClient = restClient;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DiscordGuildRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null || !ulong.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Discord user id claim missing or invalid.");
            return;
        }

        var guildIds = await GetBotGuildIdsAsync();
        if (guildIds.Count == 0)
        {
            _logger.LogWarning("Bot guild list is empty; denying access.");
            return;
        }

        foreach (var guildId in guildIds)
        {
            if (await IsUserInGuildAsync(guildId, userId))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    private async Task<IReadOnlyCollection<ulong>> GetBotGuildIdsAsync()
    {
        var guildIds = new HashSet<ulong>();
        await foreach (var guild in _restClient.GetCurrentUserGuildsAsync())
        {
            guildIds.Add(guild.Id);
        }

        return guildIds;
    }

    private async Task<bool> IsUserInGuildAsync(ulong guildId, ulong userId)
    {
        try
        {
            await _restClient.GetGuildUserAsync(guildId, userId);
            return true;
        }
        catch (RestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (RestException ex)
        {
            _logger.LogError(ex, "Failed to validate membership for user {UserId} in guild {GuildId}.", userId, guildId);
            return false;
        }
    }
}
