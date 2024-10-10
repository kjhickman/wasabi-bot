using System.Text.Json;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Settings;

namespace WasabiBot.Interfaces;

public abstract class DeferredCommandHandler : CommandHandler
{
    private readonly IAmazonSQS _sqs;
    private readonly EnvironmentVariables _env;
    
    protected DeferredCommandHandler(IAmazonSQS sqs, IOptions<EnvironmentVariables> options)
    {
        _sqs = sqs;
        _env = options.Value;
    }

    public abstract Task HandleDeferredCommand(Interaction interaction);
    
    public override async Task<InteractionResponse> HandleCommand(Interaction interaction)
    {
        var messageJson = JsonSerializer.Serialize(interaction, JsonContext.Default.Interaction);
        await _sqs.SendMessageAsync(_env.DISCORD_DEFERRED_EVENT_QUEUE_URL, messageJson);

        return new InteractionResponse
        {
            Type = InteractionResponseType.DeferredChannelMessageWithSource
        };
    }
}