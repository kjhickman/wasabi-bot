using System.Text.Json.Serialization;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess;

[JsonSerializable(typeof(DeferredInteractionMessage))]
[JsonSerializable(typeof(InteractionReceivedMessage))]
public partial class DataAccessJsonContext : JsonSerializerContext;