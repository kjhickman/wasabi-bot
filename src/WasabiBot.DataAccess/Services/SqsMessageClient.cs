using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.SQS;
using Amazon.SQS.Model;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Services;

public class SqsMessageClient : IMessageClient
{
    private readonly IAmazonSQS _sqs;

    public SqsMessageClient(IAmazonSQS sqs)
    {
        _sqs = sqs;
    }

    public async Task SendAsync<T>(T message) where T : class
    {
        var messageTypeRegistered = QueueInfo.UrlMap.TryGetValue(typeof(T).Name, out var queueUrl);
        if (!messageTypeRegistered || queueUrl is null)
        {
            throw new Exception($"Message type not registered: {typeof(T).Name}");
        }
        
        var jsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T> 
                           ?? throw new Exception("Missing JsonTypeInfo: " + typeof(T).Name);
        
        var request = new SendMessageRequest
        {
            MessageAttributes = null,
            MessageBody = JsonSerializer.Serialize(message, jsonTypeInfo),
            QueueUrl = queueUrl
        };

        await _sqs.SendMessageAsync(request);
    }
}