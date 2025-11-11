using System.Text.Json.Serialization;

namespace WasabiBot.Api.Features.Token;

public class TokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}
