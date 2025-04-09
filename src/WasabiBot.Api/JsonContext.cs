using System.Text.Json.Serialization;
using NetCord;
using NetCord.JsonModels;
using WasabiBot.Api.Modules;

namespace WasabiBot.Api;

[JsonSerializable(typeof(JsonInteractionData))]
public partial class JsonContext : JsonSerializerContext;
