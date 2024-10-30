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
    private readonly ILogger<InteractionService> _logger;

    public InteractionService(IServiceProvider serviceProvider, IDiscordService discordService, ILogger<InteractionService> logger)
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

        var command = _serviceProvider.GetKeyedService<IDiscordCommand>(commandName);
        if (command is null)
        {
            return Result.Fail<InteractionResponse>($"Handler not found for command: {commandName}");
        }
        
        var createdAt = SnowflakeHelper.ConvertToDateTimeOffset(long.Parse(interaction.Id));
        var expiration = createdAt + TimeSpan.FromMilliseconds(2500);
        using var cts = new CancellationTokenSource(expiration - DateTimeOffset.UtcNow);;

        var result = await command.Execute(interaction, cts.Token);
        if (result.IsFailed && result.HasError(x => x.Message == ""))
        {
            // todo: publish message
            return InteractionResponse.Defer();
        }

        return result;
    }

    public async Task<Result> HandleDeferredInteraction(Interaction interaction, CancellationToken ct = default)
    {
        _logger.LogInformation("Handling deferred interaction");
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            return Result.Fail("Invalid Interaction Data: missing command name");
        }
        
        var command = _serviceProvider.GetKeyedService<IDiscordCommand>(commandName);
        if (command is null)
        {
            return Result.Fail($"Handler not found for command: {commandName}");
        }
        
        _logger.LogInformation("Executing command: {CommandName}", commandName);
        var result = await command.Execute(interaction, ct);
        if (result.IsFailed)
        {
            return result.DropValue();
        }
        
        _logger.LogInformation("Creating followup message for command: {CommandName}", commandName);
        return await _discordService.CreateFollowupMessage(interaction.Token, result.Value.Data!);
    }
}
