using NetCord;
using NetCord.Hosting.Gateway;
using WasabiBot.Api.Extensions;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.EventHandlers;

internal sealed class InteractionCreatedEventHandler(IServiceProvider sp) : IInteractionCreateGatewayHandler
{
    public async ValueTask HandleAsync(Interaction interaction)
    {
        await using var scope = sp.CreateAsyncScope();
        var interactionService = scope.ServiceProvider.GetRequiredService<IInteractionService>();
        var entity = interaction.ToEntity();
        await interactionService.CreateAsync(entity);
    }
}
