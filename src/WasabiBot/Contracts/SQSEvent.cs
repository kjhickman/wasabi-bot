using System.Text.Json.Serialization;

namespace WasabiBot.Contracts;

public class SqsEvent
{
    [JsonPropertyName("Records")]
    public required SqsRecord[] Records { get; set; }
}