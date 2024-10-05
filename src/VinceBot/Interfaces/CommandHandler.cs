using VinceBot.Discord;

namespace VinceBot.Interfaces;

public abstract class CommandHandler
{
    public abstract Task<InteractionResponse> HandleCommand(Interaction interaction);
}
