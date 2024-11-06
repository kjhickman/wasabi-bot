using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public class InMemoryMessageClient : IMessageClient
{
    public Task SendAsync<T>(T message) where T : class
    {
        var messageTypeRegistered = QueueInfo.UrlMap.TryGetValue(typeof(T).Name, out var queueUrl);
        if (!messageTypeRegistered || queueUrl is null)
        {
            throw new Exception($"Message type not registered: {typeof(T).Name}");
        }

        InMemoryMessageBus.EnqueueMessage(queueUrl, message);
        return Task.CompletedTask;
    }
}