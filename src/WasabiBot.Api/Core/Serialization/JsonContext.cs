using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord.JsonModels;
using WasabiBot.Api.Features.ApiCredentials;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;

namespace WasabiBot.Api.Core.Serialization;

[JsonSerializable(typeof(ApiCredentialSummary[]))]
[JsonSerializable(typeof(CachedApiCredential))]
[JsonSerializable(typeof(JsonInteractionData))]
[JsonSerializable(typeof(List<RadioBrowserStation>))]
[JsonSerializable(typeof(MusicFavoriteRadioMetadata))]
[JsonSerializable(typeof(MusicFavoriteSongMetadata))]
internal partial class JsonContext : JsonSerializerContext;
