using VinceBot.Discord;

namespace VinceBot.Interfaces;

public interface ICommandHandler
{
    static string Name { get; } = null!;
    Task<InteractionResponse> HandleCommand(Interaction interaction);
}
