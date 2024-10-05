using VinceBot.Discord;

namespace VinceBot.Interfaces;

public interface IDiscordService
{
    Task RegisterGuildCommands(string guildId);
    Task RegisterGlobalCommands();
    Task CreateFollowupMessage(string token, string message);
}
