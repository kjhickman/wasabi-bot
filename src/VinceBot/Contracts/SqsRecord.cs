using System.Text.Json.Serialization;

namespace VinceBot.Contracts;

public class SqsRecord
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }
}
