using FluentResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Commands.Handlers;

public class PingCommand : IDiscordCommand
{
    public static string Name => "ping";
    
    public Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct)
    {
        return Task.FromResult(Result.Ok(new InteractionResponse
        {
            Type = InteractionResponseType.ChannelMessageWithSource,
            Data = new InteractionResponseData
            {
                MessageContent = "Pong!"
            }
        }));
    }
}
