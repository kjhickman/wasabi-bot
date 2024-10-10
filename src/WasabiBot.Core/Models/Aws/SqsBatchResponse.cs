using System.Collections.Concurrent;

namespace WasabiBot.Core.Models.Aws;

public class SqsBatchResponse
{
    public ConcurrentBag<BatchItemFailure> BatchItemFailures { get; set; } = new();

    public void AddFailedMessageId(string messageId)
    {
        BatchItemFailures.Add(new BatchItemFailure { ItemIdentifier = messageId });
    }
}

public class BatchItemFailure
{
    public required string ItemIdentifier { get; set; }
}
