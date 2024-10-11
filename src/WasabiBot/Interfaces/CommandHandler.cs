using WasabiBot.Core;
using WasabiBot.Core.Discord;

namespace WasabiBot.Interfaces;

public abstract class CommandHandler
{
    public abstract Task<Result<InteractionResponse>> HandleCommand(Interaction interaction);
}
