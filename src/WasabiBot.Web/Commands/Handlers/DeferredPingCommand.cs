using FluentResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Extensions;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Commands.Handlers;

public class DeferredPingCommand : IDiscordCommand
{
    public static string Name => "deferping";
    
    public async Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct)
    {
        var result = await Task.Delay(3000, ct).Try();
        if (result.IsFailed)
        {
            return result;
        }
        
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
