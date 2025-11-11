using System.Text.Json.Serialization;

namespace WasabiBot.Api.Features.Interactions;

public class GetInteractionsResponse
{
    [JsonPropertyName("interactions")]
    public required IEnumerable<InteractionDto> Interactions { get; set; }
}
