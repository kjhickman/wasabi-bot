using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Interfaces;

namespace WasabiBot.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;

    public InteractionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<InteractionResponse>> HandleInteraction(Interaction interaction)
    {
        try
        {
            if (interaction.Type == InteractionType.Ping)
            {
                return InteractionResponse.Pong();
            }

            var commandName = interaction.Data?.Name;
            if (commandName is null)
            {
                return Result<InteractionResponse>.Fail("Invalid Interaction Data: missing command name");
            }

            var handler = _serviceProvider.GetKeyedService<CommandHandler>(commandName);
            if (handler is null)
            {
                return Result<InteractionResponse>.Fail($"Handler not found for command: {commandName}");
            }

            return await handler.HandleCommand(interaction);
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public async Task<Result> HandleDeferredInteraction(Interaction interaction)
    {
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            return Result.Fail("Invalid Interaction Data: missing command name");
        }

        if (_serviceProvider.GetKeyedService<CommandHandler>(commandName) is not DeferredCommandHandler handler)
        {
            return Result.Fail($"Handler not found for command: {commandName}");
        }

        return await handler.HandleDeferredCommand(interaction);
    }
}
