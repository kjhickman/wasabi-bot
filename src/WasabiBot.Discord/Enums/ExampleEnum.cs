using Discord.Interactions;

namespace WasabiBot.Discord.Enums;

public enum ExampleEnum
{
    First,
    Second,
    Third,
    Fourth,
    [ChoiceDisplay("Twenty First")]
    TwentyFirst
}