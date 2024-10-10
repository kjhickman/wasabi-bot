using System.Text.Json.Serialization;
using WasabiBot.Core.Discord.Enums;

namespace WasabiBot.Core.Discord;

public class InteractionResponse
{
    [JsonPropertyName("type")]
    public InteractionResponseType Type { get; set; }

    [JsonPropertyName("data")]
    public InteractionResponseData? Data { get; set; }

    public static InteractionResponse Pong()
    {
        return new InteractionResponse { Type = InteractionResponseType.Pong };
    }
}
