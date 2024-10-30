using FluentResults;
using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IDiscordCommand
{
    Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct);
}