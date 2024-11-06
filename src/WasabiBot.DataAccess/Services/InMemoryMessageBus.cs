using System.Collections.Concurrent;

namespace WasabiBot.DataAccess.Services;

public class InMemoryMessageBus
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<object>> Queues = new();

    public static void EnsureQueueExists(string queueName)
    {
        // todo: move to di
        Queues.TryAdd(queueName, new ConcurrentQueue<object>());
    }

    public static void EnqueueMessage<T>(string queueName, T message) where T : class
    {
        var queue = Queues.GetOrAdd(queueName, _ => new ConcurrentQueue<object>());
        queue.Enqueue(message);
    }

    public static IEnumerable<T> DequeueMessages<T>(string queueName, int maxMessages) where T : class
    {
        if (!Queues.TryGetValue(queueName, out var queue))
        {
            return [];
        }

        var messages = new List<T>();
        for (var i = 0; i < maxMessages; i++)
        {
            if (queue.TryDequeue(out var message) && message is T typedMessage)
            {
                messages.Add(typedMessage);
            }
        }

        return messages;
    }
}