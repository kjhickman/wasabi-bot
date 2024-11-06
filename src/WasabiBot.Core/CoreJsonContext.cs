using System.Text.Json.Serialization;
using WasabiBot.Core.Discord;

namespace WasabiBot.Core;

[JsonSerializable(typeof(InteractionData))]
[JsonSerializable(typeof(ApplicationCommand[]))]
public partial class CoreJsonContext : JsonSerializerContext;