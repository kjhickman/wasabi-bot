using System.Text.Json.Serialization;

namespace WasabiBot.Core.Models.Aws;

public class SqsRecord
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
    
    [JsonPropertyName("messageAttributes")]
    public required Dictionary<string, MessageAttributeValue> MessageAttributes { get; set; }
}

public class MessageAttributeValue
{
    [JsonPropertyName("stringValue")]
    public required string StringValue { get; set; }
}
