using Microsoft.Extensions.Options;
using WasabiBot.Commands;
using WasabiBot.Core;
using WasabiBot.Core.Discord;
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
        try
        {
            var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/guilds/{guildId}/commands";
            await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public async Task<Result> RegisterGlobalCommands()
    {
        try
        {
            var url = $"https://discord.com/api/v10/applications/{_env.DISCORD_APPLICATION_ID}/commands";
            await _http.PutAsJsonAsync(url, Constants.Definitions, JsonContext.Default.ApplicationCommandArray);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public async Task<Result> CreateFollowupMessage(string token, string message)
    {
        try
        {
            var data = new InteractionResponseData
            {
                MessageContent = message
            };
        
            var url = $"https://discord.com/api/v10/webhooks/{_env.DISCORD_APPLICATION_ID}/{token}";
            await _http.PostAsJsonAsync(url, data, JsonContext.Default.InteractionResponseData);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
