using WasabiBot.Core;

namespace WasabiBot.Interfaces;

public interface IDiscordService
{
    Task<Result> RegisterGuildCommands(string guildId);
    Task<Result> RegisterGlobalCommands();
    Task<Result> CreateFollowupMessage(string token, string message);
}
