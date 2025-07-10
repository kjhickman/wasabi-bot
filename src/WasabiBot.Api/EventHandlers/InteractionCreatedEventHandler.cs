using NetCord;
using NetCord.Hosting.Gateway;
using WasabiBot.Api.Extensions;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.EventHandlers;

internal sealed class InteractionCreatedEventHandler(IServiceProvider provider) : IInteractionCreateGatewayHandler
{
    public async ValueTask HandleAsync(Interaction interaction)
    {
        var entity = interaction.ToEntity();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();

        await repository.CreateAsync(entity);
    }
}
