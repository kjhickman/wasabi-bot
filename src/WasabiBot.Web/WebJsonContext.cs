using System.Text.Json.Serialization;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Models.Contracts;

namespace WasabiBot.Web;

[JsonSerializable(typeof(Interaction))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(ApplicationCommandRegisterRequest))]
[JsonSerializable(typeof(RegisterCommandsRequest))]
public partial class WebJsonContext : JsonSerializerContext;
