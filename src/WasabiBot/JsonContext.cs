using System.Text.Json.Serialization;
using WasabiBot.Contracts;
using WasabiBot.Discord;

namespace WasabiBot;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(ApplicationCommand[]))]
[JsonSerializable(typeof(ApplicationCommandRegisterRequest))]
[JsonSerializable(typeof(RegisterCommandsRequest))]
[JsonSerializable(typeof(SqsEvent))]
[JsonSerializable(typeof(SqsBatchResponse))]
public partial class JsonContext : JsonSerializerContext;
