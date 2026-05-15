using System.Text.Json.Serialization;
using NetCord.JsonModels;
using WasabiBot.Api.Core.Responses;
using WasabiBot.Api.Features.Auth;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.OAuth;

namespace WasabiBot.Api.Core.Serialization;

[JsonSerializable(typeof(JsonInteractionData))]
[JsonSerializable(typeof(CurrentUserResponse))]
[JsonSerializable(typeof(InteractionDto))]
[JsonSerializable(typeof(GetInteractionsResponse))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class JsonContext : JsonSerializerContext;
