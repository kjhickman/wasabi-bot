using System.Text.Json.Serialization;
using WasabiBot.Core.Discord;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.DataAccess;

[JsonSerializable(typeof(InteractionDeferredMessage))]
[JsonSerializable(typeof(InteractionReceivedMessage))]
public partial class DataAccessJsonContext : JsonSerializerContext;