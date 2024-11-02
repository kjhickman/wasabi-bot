using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Commands.Handlers;

public class PingCommand : IDiscordCommand
{
    public static string Name => "ping";
    
    // TODO: support synchronous commands
    public Task<InteractionResponse> Execute(Interaction interaction, CancellationToken ct)
    {
        return Task.FromResult(InteractionResponse.Reply("Pong!"));
    }
}
