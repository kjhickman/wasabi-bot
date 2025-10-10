using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required]
    public string? ApiKey { get; set; }

    [Required]
    [Url]
    public string Endpoint { get; set; } = AIConstants.Endpoints.Gemini;

    [Required]
    public string Model { get; set; } = AIConstants.Models.GeminiFlashLite;
}
