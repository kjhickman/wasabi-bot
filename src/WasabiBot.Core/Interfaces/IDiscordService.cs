using WasabiBot.Core.Discord;

namespace WasabiBot.Core.Interfaces;

public interface IDiscordService
{
    Task CreateFollowupMessage(string token, InteractionResponseData data);
}
