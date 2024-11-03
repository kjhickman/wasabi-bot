using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IDiscordCommand
{
    Task<InteractionResponse> Execute(Interaction interaction, CancellationToken ct);
}