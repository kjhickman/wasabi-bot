using System.Text.Json;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Settings;

namespace VinceBot.Interfaces;

public abstract class DeferredCommandHandler : CommandHandler
{
    private readonly IAmazonSQS _sqs;
    private readonly DiscordSettings _settings;
    
    protected DeferredCommandHandler(IAmazonSQS sqs, IOptions<DiscordSettings> options)
    {
        _sqs = sqs;
        _settings = options.Value;
    }

    public abstract Task HandleDeferredCommand(Interaction interaction);
    
    public override async Task<InteractionResponse> HandleCommand(Interaction interaction)
    {
        var messageJson = JsonSerializer.Serialize(interaction, JsonContext.Default.Interaction);
        await _sqs.SendMessageAsync(_settings.DeferredEventQueueUrl, messageJson);

        return new InteractionResponse
        {
            Type = InteractionResponseType.DeferredChannelMessageWithSource
        };
    }
}