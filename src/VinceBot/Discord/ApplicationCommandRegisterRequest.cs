using System.Text.Json.Serialization;

namespace VinceBot.Discord;

public class ApplicationCommandRegisterRequest
{
    [JsonPropertyName("commands")]
    public required ApplicationCommand[] Commands { get; set; }
}
