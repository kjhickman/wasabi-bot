using WasabiBot.Discord;
using WasabiBot.Discord.Enums;
using WasabiBot.Interfaces;

namespace WasabiBot.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InteractionService> _logger;

    public InteractionService(IServiceProvider serviceProvider, ILogger<InteractionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<InteractionResponse?> HandleInteraction(Interaction interaction)
    {
        if (interaction.Type == InteractionType.Ping)
        {
            _logger.LogInformation("ACKing ping request");
            return InteractionResponse.Pong();
        }

        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            _logger.LogError("Invalid Interaction Data: missing command name");
            return null;
        }

        var handler = _serviceProvider.GetKeyedService<CommandHandler>(commandName);
        if (handler is null)
        {
            _logger.LogError("Handler not found for command: {commandName}", commandName);
            return null;
        }

        _logger.LogInformation("Handling interaction: {commandName}", commandName);
        return await handler.HandleCommand(interaction);
    }

    public async Task HandleDeferredInteraction(Interaction interaction)
    {
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            _logger.LogError("Invalid Interaction Data: missing command name");
            throw new Exception("Command name is required.");
        }

        if (_serviceProvider.GetKeyedService<CommandHandler>(commandName) is not DeferredCommandHandler handler)
        {
            _logger.LogError("Handler not found for command: {commandName}", commandName);
            throw new Exception($"Handler not found for deferred command: {commandName}.");
        }

        _logger.LogInformation("Handling deferred interaction: {commandName}", commandName);
        await handler.HandleDeferredCommand(interaction);
    }
}
