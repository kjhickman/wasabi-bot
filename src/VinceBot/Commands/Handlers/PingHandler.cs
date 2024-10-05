using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Interfaces;

namespace VinceBot.Commands.Handlers;

public class PingHandler : CommandHandler
{
    public static string Name => "ping";

    public override Task<InteractionResponse> HandleCommand(Interaction interaction)
    {
        return Task.FromResult(new InteractionResponse
        {
            Type = InteractionResponseType.ChannelMessageWithSource,
            Data = new InteractionResponseData
            {
                MessageContent = "Pong!"
            }
        });
    }
}
