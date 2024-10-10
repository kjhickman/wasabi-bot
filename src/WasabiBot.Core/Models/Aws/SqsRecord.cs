using System.Text.Json.Serialization;

namespace WasabiBot.Core.Models.Aws;

public class SqsRecord
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
}
