using System.Text.Json.Serialization;

namespace WasabiBot.Discord.Api;

public class User
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("discriminator")]
    public required string Discriminator { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }
}