using WasabiBot.Discord;

namespace WasabiBot.Interfaces;

public interface IDiscordService
{
    Task RegisterGuildCommands(string guildId);
    Task RegisterGlobalCommands();
    Task CreateFollowupMessage(string token, string message);
}
