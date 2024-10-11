using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Models;

namespace WasabiBot.Web.Commands.Handlers;

public class PingCommand : CommandBase
{
    public static string Name => "ping";
    
    public override Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct)
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
