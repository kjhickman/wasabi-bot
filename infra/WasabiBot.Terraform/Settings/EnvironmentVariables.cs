// ReSharper disable InconsistentNaming

using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Terraform.Settings;

public class EnvironmentVariables
{
    [Required]
    public string ENVIRONMENT { get; set; } = null!;
}
