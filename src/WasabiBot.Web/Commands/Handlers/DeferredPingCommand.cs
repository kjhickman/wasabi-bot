using FluentResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;

namespace WasabiBot.Web.Commands.Handlers;

public class DeferredPingCommand : CommandBase
{
    public static string Name => "deferping";
    
    public override async Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct)
    {
        await Task.Delay(3000, ct);
        
        return new InteractionResponse
        {
            Type = InteractionResponseType.ChannelMessageWithSource,
            Data = new InteractionResponseData
            {
                MessageContent = "Deferred pong!"
            }
        };
    }
}
