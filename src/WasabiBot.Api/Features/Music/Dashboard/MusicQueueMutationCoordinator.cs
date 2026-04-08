using System.Collections.Concurrent;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicQueueMutationCoordinator : IMusicQueueMutationCoordinator
{
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

    public async Task<T> ExecuteAsync<T>(ulong guildId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(guildId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
