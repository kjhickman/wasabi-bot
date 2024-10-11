using System.Text.Json.Serialization;
using WasabiBot.Core.Discord;

namespace WasabiBot.Core;

[JsonSerializable(typeof(InteractionData))]
public partial class CoreJsonContext : JsonSerializerContext;