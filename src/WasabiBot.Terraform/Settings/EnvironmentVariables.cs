// ReSharper disable InconsistentNaming

using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Terraform.Settings;

public class EnvironmentVariables
{
    [Required]
    public string ARCHITECTURE { get; set; } = null!;
    
    [Required]
    public string AWS_ACCOUNT_ID { get; set; } = null!;
    
    [Required]
    public string DISCORD_PUBLIC_KEY { get; set; } = null!;
    
    [Required]
    public string DISCORD_TOKEN { get; set; } = null!;
    
    [Required]
    public string DISCORD_APPLICATION_ID { get; set; } = null!;
    
    [Required]
    public string ENVIRONMENT { get; set; } = null!;
    
    [Required]
    public string NEON_CONNECTION_STRING { get; set; } = null!;
}
