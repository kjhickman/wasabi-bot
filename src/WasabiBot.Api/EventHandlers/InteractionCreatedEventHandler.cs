using NetCord;
using NetCord.Hosting.Gateway;
using WasabiBot.Api.Extensions;
using WasabiBot.DataAccess.Interfaces;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.EventHandlers;

internal sealed class InteractionCreatedEventHandler : IInteractionCreateGatewayHandler
{
    private readonly IServiceProvider _sp;
    private readonly Tracer _tracer;

    public InteractionCreatedEventHandler(IServiceProvider sp, Tracer tracer)
    {
        _sp = sp;
        _tracer = tracer;
    }

    public async ValueTask HandleAsync(Interaction interaction)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(InteractionCreatedEventHandler)}.{nameof(HandleAsync)}");
        await using var scope = _sp.CreateAsyncScope();
        var interactionService = scope.ServiceProvider.GetRequiredService<IInteractionService>();
        var entity = interaction.ToEntity();
        await interactionService.CreateAsync(entity);
    }
}
