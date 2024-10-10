using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Interfaces;

namespace WasabiBot.Commands.Handlers;

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
