using VinceBot.Commands.Handlers;
using VinceBot.Discord;
using VinceBot.Discord.Enums;

namespace VinceBot.Commands;

public class CommandConstants
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
