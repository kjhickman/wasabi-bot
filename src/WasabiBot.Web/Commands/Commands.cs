using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Commands;

public static class Commands
{
    public static readonly ApplicationCommand[] Definitions =
    [
        new()
        {
            Name = PingCommand.Name,
            Description = "Receive a pong",
            Type = ApplicationCommandType.ChatInput
        },
        new()
        {
            Name = DeferredPingCommand.Name,
            Description = "Receive a deferred pong",
            Type = ApplicationCommandType.ChatInput
        }
    ];
}
