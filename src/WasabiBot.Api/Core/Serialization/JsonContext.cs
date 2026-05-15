using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NetCord.JsonModels;
using WasabiBot.Api.Features.Auth;
using WasabiBot.Api.Features.ApiCredentials;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.OAuth;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Core.Serialization;

[JsonSerializable(typeof(ApiCredentialSummary[]))]
[JsonSerializable(typeof(CachedApiCredential))]
[JsonSerializable(typeof(JsonInteractionData))]
[JsonSerializable(typeof(List<RadioBrowserStation>))]
[JsonSerializable(typeof(MusicFavoriteRadioMetadata))]
[JsonSerializable(typeof(MusicFavoriteSongMetadata))]
[JsonSerializable(typeof(OAuthTokenRequest))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(SortDirection))]
[JsonSerializable(typeof(SortDirection?))]
[JsonSerializable(typeof(GetInteractionsResponse))]
[JsonSerializable(typeof(InteractionDto))]
[JsonSerializable(typeof(CurrentUserResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal partial class JsonContext : JsonSerializerContext;
