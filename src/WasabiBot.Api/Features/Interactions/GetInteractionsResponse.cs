using System.Text.Json.Serialization;

namespace WasabiBot.Api.Features.Interactions;

public class GetInteractionsResponse
{
    [JsonPropertyName("interactions")]
    public required IEnumerable<InteractionDto> Interactions { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
