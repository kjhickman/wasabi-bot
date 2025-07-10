using NetCord;
using NetCord.Hosting.Gateway;
using WasabiBot.Api.Extensions;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.EventHandlers;

internal sealed class InteractionCreatedEventHandler(IInteractionRepository repository)
    : IInteractionCreateGatewayHandler
{
    public async ValueTask HandleAsync(Interaction interaction)
    {
        var entity = interaction.ToEntity();
        await repository.CreateAsync(entity);
    }
}
