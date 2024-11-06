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
        using var span = _tracer.StartActiveSpan("interaction.handle");
        if (interaction.Type == InteractionType.Ping)
        {
            return InteractionResponse.Pong();
        }

        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            throw new InvalidOperationException("Invalid Interaction Data: missing command name");
        }

        var command = _serviceProvider.GetRequiredKeyedService<IDiscordCommand>(commandName);
        
        var createdAt = SnowflakeHelper.ConvertToDateTimeOffset(long.Parse(interaction.Id));
        var expiration = createdAt + TimeSpan.FromMilliseconds(2500);
        using var cts = new CancellationTokenSource(expiration - DateTimeOffset.UtcNow);

        try
        {
            var response = await command.Execute(interaction, cts.Token);
            return response;
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
        using var span = _tracer.StartActiveSpan("interaction.handle_deferred");
        _logger.LogInformation("Handling deferred interaction");
        var commandName = interaction.Data?.Name;
        if (commandName is null)
        {
            throw new InvalidOperationException("Invalid Interaction Data: missing command name");
        }
        
        var command = _serviceProvider.GetRequiredKeyedService<IDiscordCommand>(commandName);
        
        _logger.LogInformation("Executing command: {CommandName}", commandName);
        var response = await command.Execute(interaction, ct);
        
        _logger.LogInformation("Creating followup message for command: {CommandName}", commandName);
        await _discordService.CreateFollowupMessage(interaction.Token, response.Data!);
    }
}
