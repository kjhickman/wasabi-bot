using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.AI;

public class OpenRouterV2Options
{
    public const string SectionName = "OpenRouterV2";

    [Required]
    public string? ApiKey { get; set; }

    [Required]
    public required string LowLatencyPreset { get; set; }

    [Required]
    public required string LowLatencyCreativePreset { get; set; }
}
