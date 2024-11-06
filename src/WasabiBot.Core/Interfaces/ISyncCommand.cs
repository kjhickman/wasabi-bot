using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface ISyncCommand : ICommand
{
    InteractionResponse Execute(Interaction interaction);
}
