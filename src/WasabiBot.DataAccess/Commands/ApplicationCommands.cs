using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;

namespace WasabiBot.DataAccess.Commands;

public static class ApplicationCommands
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
                    Description = "Ask the magic conch a yes or no question",
                    Required = true
                }
            ]
        },
        new()
        {
            Name = WheelCommand.Name,
            Description = "Spin the wheel!",
            Type = ApplicationCommandType.ChatInput,
            Options =
            [
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option1",
                    Description = "Choose an option for the wheel",
                    Required = true
                },
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option2",
                    Description = "Choose an option for the wheel",
                    Required = true
                },
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option3",
                    Description = "Choose an option for the wheel",
                    Required = false
                },
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option4",
                    Description = "Choose an option for the wheel",
                    Required = false
                },
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option5",
                    Description = "Choose an option for the wheel",
                    Required = false
                },
                new ApplicationCommandOption
                {
                    Type = ApplicationCommandOptionType.String,
                    Name = "option6",
                    Description = "Choose an option for the wheel",
                    Required = false
                }
            ]
        }
    ];
}
