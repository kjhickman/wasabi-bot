using System.Text.Json.Serialization;
using VinceBot.Discord;

namespace VinceBot;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
public partial class JsonContext : JsonSerializerContext;
