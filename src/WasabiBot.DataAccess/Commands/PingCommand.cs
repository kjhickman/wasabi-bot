using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.DataAccess.Commands;

public class PingCommand : ISyncCommand
{
    public static string Name => "ping";

    public InteractionResponse Execute(Interaction interaction)
    {
        return InteractionResponse.Reply("Pong!");
    }
}
