using System.Text.Json;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Interfaces;
using VinceBot.Settings;

namespace VinceBot.CommandHandlers;

public class DeferredPingHandler : IDeferredCommandHandler
{
    public static string Name => "deferping";

    private readonly IAmazonSQS _sqs;
    private readonly DiscordSettings _settings;
    private readonly HttpClient _httpClient;

    public DeferredPingHandler(IAmazonSQS sqs, IOptions<DiscordSettings> options, HttpClient httpClient)
    {
        _sqs = sqs;
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<InteractionResponse> HandleCommand(Interaction interaction)
    {
        // todo: publish sqs message
        var messageJson = JsonSerializer.Serialize(interaction, JsonContext.Default.Interaction);
        await _sqs.SendMessageAsync(_settings.DeferredEventQueueUrl, messageJson);
        
        return new InteractionResponse
        {
            Type = InteractionResponseType.DeferredChannelMessageWithSource
        };
    }

    public async Task HandleDeferredCommand(Interaction interaction)
    {
        await Task.Delay(5000);
        var interactionResponse = new InteractionResponseData
        {
            MessageContent = "Deferred pong!"
        };

        var url = $"https://discord.com/api/v10/webhooks/{_settings.ApplicationId}/{interaction.Token}";
        var response = await _httpClient.PostAsJsonAsync(url, interactionResponse, JsonContext.Default.InteractionResponseData);
        Console.WriteLine(response.Content.ReadAsStringAsync().Result);
    }
}
