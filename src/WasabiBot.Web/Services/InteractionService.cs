using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Extensions;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models;
using WasabiBot.DataAccess.Messages;
using WasabiBot.Web.Commands.Handlers;

namespace WasabiBot.Web.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDiscordService _discordService;
    private readonly ILogger _logger;
    private readonly IMessageClient _messageClient;

    public InteractionService(IServiceProvider serviceProvider, IDiscordService discordService, ILogger logger, IMessageClient messageClient)
    {
        _serviceProvider = serviceProvider;
        _discordService = discordService;
        _logger = logger;
        _messageClient = messageClient;
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

            var command = _serviceProvider.GetKeyedService<CommandBase>(commandName);
            if (command is null)
            {
                return Result<InteractionResponse>.Fail($"Handler not found for command: {commandName}");
            }

            var creationTime = SnowflakeHelper.ConvertToDateTimeOffset(long.Parse(interaction.Id));
            var timeToExecute = creationTime.AddMilliseconds(2500) - DateTime.UtcNow;
            if (timeToExecute < TimeSpan.Zero)
            {
                _logger.Warning("Interaction timed out");
                var msg = DeferredInteractionMessage.FromInteraction(interaction);
                await _messageClient.SendMessage(msg);
                return InteractionResponse.Defer();
            }
            using var cts = new CancellationTokenSource(timeToExecute);

            return await command.Execute(interaction, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Interaction timed out");
            var msg = DeferredInteractionMessage.FromInteraction(interaction);
            await _messageClient.SendMessage(msg);
            return InteractionResponse.Defer();
        }
        catch (Exception e)
        {
            return e;
        }
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
        if (result.IsError)
        {
            return result.DropValue();
        }
        
        _logger.Information("Creating followup message for command: {CommandName}", commandName);
        return await _discordService.CreateFollowupMessage(interaction.Token, result.Value.Data!);
    }
}
