using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.DataAccess.Commands.Handlers;

namespace WasabiBot.DataAccess.Commands;

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
