// ReSharper disable InconsistentNaming

namespace WasabiBot.Terraform.Settings;

public class EnvironmentVariables
{
    public string ARCHITECTURE { get; set; } = null!;
    public string AWS_ACCOUNT_ID { get; set; } = null!;
    public string DISCORD_PUBLIC_KEY { get; set; } = null!;
    public string DISCORD_TOKEN { get; set; } = null!;
    public string DISCORD_APPLICATION_ID { get; set; } = null!;
    public string ENVIRONMENT { get; set; } = null!;
}
