using WasabiBot.Commands.Handlers;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;

namespace WasabiBot.Commands;

public static class Commands
{
    public static readonly ApplicationCommand[] Definitions =
    [
        new()
        {
            Name = PingHandlerBase.Name,
            Description = "Receive a pong",
            Type = ApplicationCommandType.ChatInput
        },
        new()
        {
            Name = DeferredPingHandlerBase.Name,
            Description = "Receive a deferred pong",
            Type = ApplicationCommandType.ChatInput
        }
    ];
}
