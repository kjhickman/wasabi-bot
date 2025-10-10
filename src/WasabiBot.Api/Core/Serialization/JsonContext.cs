using System.Text.Json.Serialization;
using NetCord.JsonModels;

namespace WasabiBot.Api.Core.Serialization;

[JsonSerializable(typeof(JsonInteractionData))]
internal partial class JsonContext : JsonSerializerContext;
