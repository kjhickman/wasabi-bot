using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Interfaces;

namespace VinceBot.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;

    public InteractionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<InteractionResponse> HandleInteraction(Interaction interaction)
    {
        if (interaction.Type == InteractionType.Ping)
        {
            // ACK ping
            return InteractionResponse.Pong();
        }
        
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            throw new Exception("Command name is required.");
        }
        
        var handler = _serviceProvider.GetKeyedService<ICommandHandler>(commandName);
        if (handler is null)
        {
            throw new Exception($"Handler not found for command: {commandName}.");
        }
        
        return await handler.HandleCommand(interaction);
    }
}
