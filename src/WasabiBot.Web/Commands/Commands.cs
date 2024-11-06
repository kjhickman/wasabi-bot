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
            Name = MagicConchCommand.Name,
            Description = "Ask the Magic Conch!",
            Type = ApplicationCommandType.ChatInput,
            Options =
            [
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "question",
                    Description = "What would you like to ask the Magic Conch?",
                    Required = true
                }
            ]
        },
    ];
}
