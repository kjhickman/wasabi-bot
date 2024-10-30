using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IDiscordService
{
    Task RegisterGuildCommands(string guildId);
    Task RegisterGlobalCommands();
    Task CreateFollowupMessage(string token, InteractionResponseData data);
}
