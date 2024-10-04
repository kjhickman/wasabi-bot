namespace VinceBot.Interfaces;

public interface ICommandsService
{
    Task RegisterGuildCommands(string guildId);
    Task RegisterGlobalCommands();
}
