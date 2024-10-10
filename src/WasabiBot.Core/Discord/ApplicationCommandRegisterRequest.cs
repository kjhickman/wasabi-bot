using System.Text.Json.Serialization;

namespace WasabiBot.Core.Discord;

public class ApplicationCommandRegisterRequest
{
    [JsonPropertyName("commands")]
    public required ApplicationCommand[] Commands { get; set; }
}
