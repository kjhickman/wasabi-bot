using System.Text.Json.Serialization;

namespace WasabiBot.Api.Features.Credentials;

public sealed record CreateCredentialRequest(
    [property: JsonPropertyName("name")] string Name);
