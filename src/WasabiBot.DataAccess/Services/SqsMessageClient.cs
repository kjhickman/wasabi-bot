using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using WasabiBot.Core.Extensions;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models;
using WasabiBot.DataAccess.Settings;

namespace WasabiBot.DataAccess.Services;

public class SqsMessageClient : IMessageClient
{
    private readonly IAmazonSQS _sqs;
    private readonly EnvironmentVariables _env;

    public SqsMessageClient(IAmazonSQS sqs, IOptions<EnvironmentVariables> options)
    {
        _sqs = sqs;
        _env = options.Value;
    }

    public async Task<Result> SendMessage<T>(T message) where T : IMessage
    {
        try
        {
            var jsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(typeof(T));
            if (jsonTypeInfo is null)
            {
                throw new Exception($"Missing Json Type Info for {typeof(T).Name}");
            }
        
            var request = new SendMessageRequest
            {
                MessageBody = JsonSerializer.Serialize(message, jsonTypeInfo),
                QueueUrl = _env.DISCORD_DEFERRED_EVENT_QUEUE_URL,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    { "MessageHandler", new MessageAttributeValue { StringValue = message.MessageHandlerName, DataType = "String" } }
                }
            };
        
            return await _sqs.SendMessageAsync(request).TryDropValue();
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
