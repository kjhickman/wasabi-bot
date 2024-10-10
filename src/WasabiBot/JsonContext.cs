using System.Text.Json.Serialization;
using WasabiBot.Core.Contracts;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Models.Aws;

namespace WasabiBot;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(ApplicationCommand[]))]
[JsonSerializable(typeof(ApplicationCommandRegisterRequest))]
[JsonSerializable(typeof(RegisterCommandsRequest))]
[JsonSerializable(typeof(SqsEvent))]
[JsonSerializable(typeof(SqsBatchResponse))]
public partial class JsonContext : JsonSerializerContext;
