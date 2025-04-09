using System.Text.Json.Serialization;
using NetCord;
using NetCord.JsonModels;
using WasabiBot.Api.Modules;

namespace WasabiBot.Api;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonInteractionData))]
public partial class JsonContext : JsonSerializerContext;
