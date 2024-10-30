using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Commands.Handlers;

public class DeferredPingCommand : IDiscordCommand
{
    public static string Name => "deferping";
    
    public async Task<InteractionResponse> Execute(Interaction interaction, CancellationToken ct)
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
