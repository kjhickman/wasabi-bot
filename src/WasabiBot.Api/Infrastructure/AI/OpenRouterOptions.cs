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

    [Required]
    public string DefaultPreset { get; set; } = AIConstants.Models.GeminiFlash;
}
