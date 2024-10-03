using System.Text.Json.Serialization;
using VinceBot.Discord.Enums;

namespace VinceBot.Discord;

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
