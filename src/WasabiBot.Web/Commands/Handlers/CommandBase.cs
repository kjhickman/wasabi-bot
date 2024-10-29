using FluentResults;
using WasabiBot.Core.Discord;

namespace WasabiBot.Web.Commands.Handlers;

public abstract class CommandBase
{
    public abstract Task<Result<InteractionResponse>> Execute(Interaction interaction, CancellationToken ct);
}
