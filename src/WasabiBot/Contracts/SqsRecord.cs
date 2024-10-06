using System.Text.Json.Serialization;

namespace WasabiBot.Contracts;

public class SqsRecord
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
}
