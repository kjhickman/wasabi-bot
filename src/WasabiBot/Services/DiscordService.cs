using Microsoft.Extensions.Options;
using WasabiBot.Commands;
using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Extensions;
using WasabiBot.Interfaces;
using WasabiBot.Settings;

namespace WasabiBot.Services;

public class DiscordService : IDiscordService
{
    private readonly HttpClient _http;
    private readonly EnvironmentVariables _env;

    public DiscordService(HttpClient http, IOptions<EnvironmentVariables> options)
    {
        _env = options.Value;
        _http = http;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_env.DISCORD_TOKEN}");
    }

    public async Task<Result> RegisterGuildCommands(string guildId)
    {
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/guilds/{guildId}/commands";
        return await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray).AsResult();
    }

    public async Task<Result> RegisterGlobalCommands()
    {
        var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/commands";
        return await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray).AsResult();
    }

    public async Task<Result> CreateFollowupMessage(string token, string message)
    {
        var data = new InteractionResponseData
        {
            MessageContent = message
        };
    
        var url = $"https://discord.com/api/v10/webhooks/{_env.DISCORD_APPLICATION_ID}/{token}";
        return await _http.PostAsJsonAsync(url, data, JsonContext.Default.InteractionResponseData).AsResult();
    }
}
