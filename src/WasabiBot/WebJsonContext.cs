using System.Text.Json.Serialization;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Models.Aws;
using WasabiBot.Core.Models.Contracts;

namespace WasabiBot;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(ApplicationCommand[]))]
[JsonSerializable(typeof(ApplicationCommandRegisterRequest))]
[JsonSerializable(typeof(RegisterCommandsRequest))]
[JsonSerializable(typeof(SqsEvent))]
[JsonSerializable(typeof(SqsBatchResponse))]
public partial class WebJsonContext : JsonSerializerContext;
