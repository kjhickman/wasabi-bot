using Dapper;
using OpenTelemetry.Trace;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.Web.Services;

public class InteractionService : IInteractionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDiscordService _discordService;
    private readonly ILogger<InteractionService> _logger;
    private readonly IMessageClient _messageClient;
    private readonly Tracer _tracer;

    public InteractionService(IServiceProvider serviceProvider, IDiscordService discordService,
        ILogger<InteractionService> logger, IMessageClient messageClient, Tracer tracer)
    {
        _serviceProvider = serviceProvider;
        _discordService = discordService;
        _logger = logger;
        _messageClient = messageClient;
        _tracer = tracer;
    }

    public async Task<InteractionResponse> HandleInteraction(Interaction interaction)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(InteractionService)}.{nameof(HandleInteraction)}");
        if (interaction.Type == InteractionType.Ping)
        {
            return InteractionResponse.Pong();
        }

        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            throw new InvalidOperationException("Invalid Interaction Data: missing command name");
        }

        _logger.LogInformation("Handling command: {CommandName}", commandName);
        var command = _serviceProvider.GetRequiredKeyedService<ICommand>(commandName);
        
        var createdAt = SnowflakeHelper.ConvertToDateTimeOffset(long.Parse(interaction.Id));
        var expiration = createdAt + TimeSpan.FromMilliseconds(2500);
        var timeSpan = expiration - DateTimeOffset.UtcNow;
        if (timeSpan < TimeSpan.Zero)
        {
            return InteractionResponse.Defer();
        }
        using var cts = new CancellationTokenSource(expiration - DateTimeOffset.UtcNow);

        try
        {
            return command switch
            {
                ISyncCommand syncCmd => syncCmd.Execute(interaction),
                IAsyncCommand asyncCmd => await asyncCmd.Execute(interaction, cts.Token),
                _ => throw new ArgumentException("Unknown command type", nameof(command))
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{CommandName} execution timed out", commandName);
            await _messageClient.SendAsync(InteractionDeferredMessage.FromInteraction(interaction));
            return InteractionResponse.Defer();
        }
    }

    public async Task HandleDeferredInteraction(Interaction interaction, CancellationToken ct = default)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(InteractionService)}.{nameof(HandleDeferredInteraction)}");
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            throw new InvalidOperationException("Invalid Interaction Data: missing command name");
        }
        
        _logger.LogInformation("Handling deferred command: {CommandName}", commandName);
        var command = _serviceProvider.GetRequiredKeyedService<IAsyncCommand>(commandName);
        
        var response = await command.Execute(interaction, ct);
        
        _logger.LogInformation("Creating followup message for command: {CommandName}", commandName);
        await _discordService.CreateFollowupMessage(interaction.Token, response.Data!);
    }
}
