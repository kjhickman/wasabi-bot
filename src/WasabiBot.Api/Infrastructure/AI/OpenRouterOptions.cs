using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.AI;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    [Required]
    public string? ApiKey { get; set; }

    [Required]
    [Url]
    public string Endpoint { get; set; } = AIConstants.Endpoints.OpenRouter;

    /// <summary>
    /// Model used for low-latency interactive commands.
    /// </summary>
    public string LowLatency { get; set; } = AIConstants.Presets.LowLatency;

    /// <summary>
    /// Model used for low-latency creative commands.
    /// </summary>
    public string LowLatencyCreative { get; set; } = AIConstants.Presets.LowLatencyCreative;
}