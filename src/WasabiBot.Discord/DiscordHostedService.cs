using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WasabiBot.Discord;

public class DiscordHostedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionHandler _interactionHandler;
    private readonly DiscordSettings _settings;

    public DiscordHostedService(DiscordSocketClient client, InteractionHandler interactionHandler,
        IOptions<DiscordSettings> options)
    {
        _client = client;
        _interactionHandler = interactionHandler;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += LogAsync;
        await _interactionHandler.InitializeAsync();
        
        await _client.LoginAsync(TokenType.Bot, _settings.Token);
        await _client.StartAsync();
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static Task LogAsync(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
}