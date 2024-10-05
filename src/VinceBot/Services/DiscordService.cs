using Microsoft.Extensions.Options;
using VinceBot.Commands;
using VinceBot.Discord;
using VinceBot.Interfaces;
using VinceBot.Settings;

namespace VinceBot.Services;

public class DiscordService : IDiscordService
{
    private readonly HttpClient _http;
    private readonly DiscordSettings _settings;
    private readonly ILogger<DiscordService> _logger;

    public DiscordService(HttpClient http, IOptions<DiscordSettings> options, ILogger<DiscordService> logger)
    {
        _settings = options.Value;
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_settings.Token}");
    }

    public async Task RegisterGuildCommands(string guildId)
    {
        _logger.LogInformation("Registering commands for guild {guildId}", guildId);
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/guilds/{guildId}/commands";
        await _http.PutAsJsonAsync(url, CommandConstants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task RegisterGlobalCommands()
    {
        _logger.LogInformation("Registering global commands");
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/commands";
        await _http.PutAsJsonAsync(url, CommandConstants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task CreateFollowupMessage(string token, string message)
    {
        _logger.LogInformation("Creating followup message for interaction {token}", token);
        var data = new InteractionResponseData
        {
            MessageContent = message
        };
        
        var url = $"https://discord.com/api/v10/webhooks/{_settings.ApplicationId}/{token}";
        await _http.PostAsJsonAsync(url, data, JsonContext.Default.InteractionResponseData);
    }
}
