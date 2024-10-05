using WasabiBot.Commands.Handlers;
using WasabiBot.Discord;
using WasabiBot.Discord.Enums;

namespace WasabiBot.Commands;

public static class Constants
{
    public static readonly ApplicationCommand[] Definitions =
    [
        new()
        {
            Name = PingHandler.Name,
            Description = "Receive a pong",
            Type = ApplicationCommandType.ChatInput
        },
        new()
        {
            Name = DeferredPingHandler.Name,
            Description = "Receive a deferred pong",
            Type = ApplicationCommandType.ChatInput
        }
    ];
}
