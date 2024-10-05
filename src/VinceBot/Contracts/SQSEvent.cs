using System.Text.Json.Serialization;

namespace VinceBot.Contracts;

public class SqsEvent
{
    [JsonPropertyName("Records")]
    public required SqsRecord[] Records { get; set; }
}