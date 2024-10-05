using System.Text.Json.Serialization;

namespace WasabiBot.Discord;

public class ApplicationCommandRegisterRequest
{
    [JsonPropertyName("commands")]
    public required ApplicationCommand[] Commands { get; set; }
}
