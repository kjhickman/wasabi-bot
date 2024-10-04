using VinceBot.Discord;

namespace VinceBot.Interfaces;

public interface IDeferredCommandHandler : ICommandHandler
{
    Task HandleDeferredCommand(Interaction interaction);
}
