namespace WasabiBot.Contracts;

public class RegisterCommandsRequest
{
    public string? GuildId { get; set; }
    public bool RegisterGlobalCommands { get; set; }
}
