using System.Text.Json.Serialization;

namespace VinceBot.Contracts;

public class SQSEvent
{
    [JsonPropertyName("Records")]
    public SQSRecord[] Records { get; set; }
}

public class SQSRecord
{
    [JsonPropertyName("body")]
    public string Body { get; set; }
}
