using System.Text.Json.Serialization;

namespace VinceBot.Discord;

public class InteractionResponseData
{
    [JsonPropertyName("tts")]
    public bool? TextToSpeech { get; set; }

    [JsonPropertyName("content")]
    public string? MessageContent { get; set; }

    [JsonPropertyName("embeds")]
    public Embed[]? Embeds { get; set; }

    [JsonPropertyName("allowed_mentions")]
    public AllowedMentions? AllowedMentions { get; set; }

    [JsonPropertyName("flags")]
    public int? Flags { get; set; }
}

public class Embed
{
}

public class AllowedMentions
{
    [JsonPropertyName("parse")]
    public string[]? Parse { get; set; }
}
