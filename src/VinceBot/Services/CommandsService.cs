using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VinceBot.CommandHandlers;
using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Interfaces;
using VinceBot.Settings;

namespace VinceBot.Services;

public class CommandsService : ICommandsService
{
    private readonly HttpClient _httpClient;
    private readonly DiscordSettings _settings;

    private static readonly ApplicationCommand[] Commands =
    [
        new()
        {
            Name = PingHandler.Name,
            Description = "Receive a pong",
            Type = ApplicationCommandType.ChatInput
        },
        new()
        {
            Name = DeferredPingHandler.Name,
            Description = "Receive a deferred pong",
            Type = ApplicationCommandType.ChatInput
        }
    ];

    public CommandsService(HttpClient httpClient, IOptions<DiscordSettings> options)
    {
        _settings = options.Value;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bot {_settings.Token}");
    }

    public async Task RegisterGuildCommands(string guildId)
    {
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/guilds/{guildId}/commands";
        await _httpClient.PutAsJsonAsync(url, Commands, JsonContext.Default.ApplicationCommandArray);
    }

    public async Task RegisterGlobalCommands()
    {
        var url = $"https://discord.com/api/v10/applications/{_settings.ApplicationId}/commands";
        await _httpClient.PutAsJsonAsync(url, Commands, JsonContext.Default.ApplicationCommandArray);
    }
}