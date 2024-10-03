using System.Text.Json.Serialization;

namespace VinceBot.Discord;

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("username")]
    public string Username { get; set; } = null!;

    [JsonPropertyName("discriminator")]
    public string Discriminator { get; set; } = null!;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }
}
