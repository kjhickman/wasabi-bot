using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.AI;

public class OpenRouterV2Options
{
    public const string SectionName = "OpenRouterV2";

    [Required]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model used for low-latency interactive commands.
    /// </summary>
    public required string LowLatencyPreset { get; set; }

    /// <summary>
    /// Model used for low-latency creative commands.
    /// </summary>
    public required string LowLatencyCreativePreset { get; set; }
}
