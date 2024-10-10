using System.Text.Json.Serialization;

namespace WasabiBot.Core.Models.Aws;

public class SqsEvent
{
    [JsonPropertyName("Records")]
    public required SqsRecord[] Records { get; set; }
}