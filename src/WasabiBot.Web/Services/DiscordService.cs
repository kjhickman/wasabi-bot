using Microsoft.Extensions.Options;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.Web.Settings;

namespace WasabiBot.Web.Services;

public class DiscordService : IDiscordService
{
    private readonly HttpClient _http;
    private readonly DiscordSettings _discordSettings;
    private readonly ILogger<DiscordService> _logger;

    public DiscordService(HttpClient http, IOptions<DiscordSettings> options, ILogger<DiscordService> logger)
    {
        _discordSettings = options.Value;
        _http = http;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_discordSettings.Token}");
        _logger = logger;
    }

    public async Task CreateFollowupMessage(string token, InteractionResponseData data)
    {
        _logger.LogInformation("Creating followup message for token {Token}", token);
        var url = $"https://discord.com/api/v10/webhooks/{_discordSettings.ApplicationId}/{token}";
        await _http.PostAsJsonAsync(url, data, WebJsonContext.Default.InteractionResponseData);
    }
}
