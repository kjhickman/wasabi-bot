using NetCord.Rest;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Infrastructure.Auth;

internal sealed class DiscordGuildAuthorizationClient(
    RestClient restClient,
    ILogger<DiscordGuildAuthorizationClient> logger,
    Tracer tracer)
    : IDiscordGuildAuthorizationClient
{
    public async Task<IReadOnlyCollection<ulong>> GetBotGuildIdsAsync(CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.discord-guild.fetch-bot-guilds");
        var guildIds = new HashSet<ulong>();
        await foreach (var guild in restClient.GetCurrentUserGuildsAsync())
        {
            guildIds.Add(guild.Id);
        }

        return guildIds.ToArray();
    }

    public async Task<bool> IsUserInGuildAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        using var span = tracer.StartActiveSpan("auth.discord-guild.fetch-membership");
        try
        {
            await restClient.GetGuildUserAsync(guildId, userId, cancellationToken: cancellationToken);
            return true;
        }
        catch (RestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (RestException ex)
        {
            span.RecordException(ex);
            logger.LogError(ex, "Failed to validate membership for user {UserId} in guild {GuildId}.", userId, guildId);
            return false;
        }
    }
}
