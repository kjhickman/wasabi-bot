using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Interfaces;

namespace VinceBot.CommandHandlers;

public class PingHandler : ICommandHandler
{
    public static string Name => "ping";

    public Task<InteractionResponse> HandleCommand(Interaction interaction)
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
