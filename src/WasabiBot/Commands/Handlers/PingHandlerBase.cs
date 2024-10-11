using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Models;

namespace WasabiBot.Commands.Handlers;

public class PingHandlerBase : CommandHandlerBase
{
    public static string Name => "ping";

    public override Task<Result<InteractionResponse>> HandleCommand(Interaction interaction)
    {
        return Task.FromResult(Result<InteractionResponse>.Ok(new InteractionResponse
        {
            Type = InteractionResponseType.ChannelMessageWithSource,
            Data = new InteractionResponseData
            {
                MessageContent = "Pong!"
            }
        }));
    }
}
