using WasabiBot.Discord;

namespace WasabiBot.Interfaces;

public abstract class CommandHandler
{
    public abstract Task<InteractionResponse> HandleCommand(Interaction interaction);
}
