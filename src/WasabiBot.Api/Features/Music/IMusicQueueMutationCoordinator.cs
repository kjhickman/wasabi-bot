namespace WasabiBot.Api.Features.Music;

internal interface IMusicQueueMutationCoordinator
{
    Task<T> ExecuteAsync<T>(ulong guildId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
