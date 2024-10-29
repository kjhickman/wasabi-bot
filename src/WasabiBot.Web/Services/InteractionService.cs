using FluentResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Extensions;
using WasabiBot.Core.Interfaces;
using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDiscordService _discordService;
    private readonly ILogger _logger;

    public InteractionService(IServiceProvider serviceProvider, IDiscordService discordService, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _discordService = discordService;
        _logger = logger;
    }

    public async Task<Result<InteractionResponse>> HandleInteraction(Interaction interaction)
    {
        if (interaction.Type == InteractionType.Ping)
        {
            return InteractionResponse.Pong();
        }

        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            return Result.Fail<InteractionResponse>("Invalid Interaction Data: missing command name");
        }

        var command = _serviceProvider.GetKeyedService<CommandBase>(commandName);
        if (command is null)
        {
            return Result.Fail<InteractionResponse>($"Handler not found for command: {commandName}");
        }

        return await command.Execute(interaction, CancellationToken.None);
    }

    public async Task<Result> HandleDeferredInteraction(Interaction interaction, CancellationToken ct = default)
    {
        _logger.Information("Handling deferred interaction");
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            return Result.Fail("Invalid Interaction Data: missing command name");
        }
        
        var command = _serviceProvider.GetKeyedService<CommandBase>(commandName);
        if (command is null)
        {
            return Result.Fail($"Handler not found for command: {commandName}");
        }
        
        _logger.Information("Executing command: {CommandName}", commandName);
        var result = await command.Execute(interaction, ct);
        if (result.IsFailed)
        {
            return result.DropValue();
        }
        
        _logger.Information("Creating followup message for command: {CommandName}", commandName);
        return await _discordService.CreateFollowupMessage(interaction.Token, result.Value.Data!);
    }
}
