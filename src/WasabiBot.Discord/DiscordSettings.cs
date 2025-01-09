namespace WasabiBot.Discord;

public class DiscordSettings
{
    public required string PublicKey { get; set; }
    public required string Token { get; set; }
    public ulong? TestGuildId { get; set; }
}