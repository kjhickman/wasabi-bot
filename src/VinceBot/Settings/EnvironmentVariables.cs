// ReSharper disable InconsistentNaming
namespace VinceBot.Settings;

public class EnvironmentVariables
{
    public string ENVIRONMENT { get; set; } = null!;
    public string DISCORD_PUBLIC_KEY { get; set; } = null!;
    public string DISCORD_TOKEN { get; set; } = null!;
    public string DISCORD_APPLICATION_ID { get; set; } = null!;
    public string DISCORD_DEFERRED_EVENT_QUEUE_URL { get; set; } = null!;
}
