using System.Text.Json.Serialization;
using VinceBot.Contracts;
using VinceBot.Discord;

namespace VinceBot;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(ApplicationCommand[]))]
[JsonSerializable(typeof(ApplicationCommandRegisterRequest))]
[JsonSerializable(typeof(RegisterCommandsRequest))]
[JsonSerializable(typeof(SQSEvent))]
public partial class JsonContext : JsonSerializerContext;
