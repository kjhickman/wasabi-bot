using WasabiBot.Core.Discord;
using WasabiBot.Core.Models;

namespace WasabiBot.Core.Interfaces;

public interface IDiscordService
{
    Task<Result> RegisterGuildCommands(string guildId);
    Task<Result> RegisterGlobalCommands();
    Task<Result> CreateFollowupMessage(string token, InteractionResponseData data);
}
