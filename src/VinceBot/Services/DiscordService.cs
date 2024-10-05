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

    public DiscordService(HttpClient http, IOptions<DiscordSettings> options)
    {
        _settings = options.Value;
        _http = http;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_settings.Token}");
    }

    public async Task RegisterGuildCommands(string guildId)
    {
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/guilds/{guildId}/commands";
        await _http.PutAsJsonAsync(url, CommandConstants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task RegisterGlobalCommands()
    {
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/commands";
        await _http.PutAsJsonAsync(url, CommandConstants.Definitions, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task CreateFollowupMessage(string token, string message)
    {
        var data = new InteractionResponseData
        {
            MessageContent = message
        };
        
        var url = $"https://discord.com/api/v10/webhooks/{_settings.ApplicationId}/{token}";
        await _http.PostAsJsonAsync(url, data, JsonContext.Default.InteractionResponseData);
    }
}
