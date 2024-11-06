using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IAsyncCommand : ICommand
{
    Task<InteractionResponse> Execute(Interaction interaction, CancellationToken ct);
}
