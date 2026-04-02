namespace WasabiBot.Api.Infrastructure.Auth;

internal interface IDiscordGuildAuthorizationClient
{
    Task<IReadOnlyCollection<ulong>> GetBotGuildIdsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsUserInGuildAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default);
}
