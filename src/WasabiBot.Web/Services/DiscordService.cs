using Microsoft.Extensions.Options;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Settings;

namespace WasabiBot.Web.Services;

public class DiscordService : IDiscordService
{
    private readonly HttpClient _http;
    private readonly EnvironmentVariables _env;
    private readonly ILogger<DiscordService> _logger;

    public DiscordService(HttpClient http, IOptions<EnvironmentVariables> options, ILogger<DiscordService> logger)
    {
        _env = options.Value;
        _http = http;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_env.DISCORD_TOKEN}");
        _logger = logger;
    }

    public async Task RegisterGuildCommands(string guildId)
    {
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/guilds/{guildId}/commands";
        await _http.PutAsJsonAsync(url, Commands.Commands.Definitions, WebJsonContext.Default.ApplicationCommandArray);
    }

    public async Task RegisterGlobalCommands()
    {
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/commands";
        await _http.PutAsJsonAsync(url, Commands.Commands.Definitions, WebJsonContext.Default.ApplicationCommandArray);
    }

    public async Task CreateFollowupMessage(string token, InteractionResponseData data)
    {
        _logger.LogInformation("Creating followup message for token {Token}", token);
        var url = $"https://discord.com/api/v10/webhooks/{_env.DISCORD_APPLICATION_ID}/{token}";
        await _http.PostAsJsonAsync(url, data, WebJsonContext.Default.InteractionResponseData);
    }
}
