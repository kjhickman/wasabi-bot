using Microsoft.Extensions.Options;
using WasabiBot.Commands;
using WasabiBot.Discord;
using WasabiBot.Interfaces;
using WasabiBot.Settings;

namespace WasabiBot.Services;

public class DiscordService : IDiscordService
{
    private readonly HttpClient _http;
    private readonly EnvironmentVariables _env;
    private readonly ILogger<DiscordService> _logger;

    public DiscordService(HttpClient http, IOptions<EnvironmentVariables> options, ILogger<DiscordService> logger)
    {
        _env = options.Value;
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_env.DISCORD_TOKEN}");
    }

    public async Task RegisterGuildCommands(string guildId)
    {
        _logger.LogInformation("Registering commands for guild {guildId}", guildId);
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/guilds/{guildId}/commands";
        await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task RegisterGlobalCommands()
    {
        _logger.LogInformation("Registering global commands");
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/commands";
        await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task CreateFollowupMessage(string token, string message)
    {
        _logger.LogInformation("Creating followup message for interaction {token}", token);
        var data = new InteractionResponseData
        {
            MessageContent = message
        };
        
        var url = $"https://discord.com/api/v10/webhooks/{_env.DISCORD_APPLICATION_ID}/{token}";
        await _http.PostAsJsonAsync(url, data, JsonContext.Default.InteractionResponseData);
    }
}
