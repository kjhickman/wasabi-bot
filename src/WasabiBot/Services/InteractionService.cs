using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Interfaces;

namespace WasabiBot.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public InteractionService(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<InteractionResponse?> HandleInteraction(Interaction interaction)
    {
        if (interaction.Type == InteractionType.Ping)
        {
            _logger.Information("ACKing ping request");
            return InteractionResponse.Pong();
        }

        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            _logger.Error("Invalid Interaction Data: missing command name");
            return null;
        }

        var handler = _serviceProvider.GetKeyedService<CommandHandler>(commandName);
        if (handler is null)
        {
            _logger.Error("Handler not found for command: {commandName}", commandName);
            return null;
        }

        _logger.Information("Handling interaction: {commandName}", commandName);
        return await handler.HandleCommand(interaction);
    }

    public async Task HandleDeferredInteraction(Interaction interaction)
    {
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            _logger.Error("Invalid Interaction Data: missing command name");
            throw new Exception("Command name is required.");
        }

        if (_serviceProvider.GetKeyedService<CommandHandler>(commandName) is not DeferredCommandHandler handler)
        {
            _logger.Error("Handler not found for command: {commandName}", commandName);
            throw new Exception($"Handler not found for deferred command: {commandName}.");
        }

        _logger.Information("Handling deferred interaction: {commandName}", commandName);
        await handler.HandleDeferredCommand(interaction);
    }
}
