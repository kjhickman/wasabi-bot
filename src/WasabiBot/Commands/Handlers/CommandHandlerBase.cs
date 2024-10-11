using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Models;

namespace WasabiBot.Commands.Handlers;

public abstract class CommandHandlerBase
{
    public abstract Task<Result<InteractionResponse>> HandleCommand(Interaction interaction);
}
