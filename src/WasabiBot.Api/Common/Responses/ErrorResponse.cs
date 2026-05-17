using System.Text.Json.Serialization;

namespace WasabiBot.Api.Core.Responses;

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);
