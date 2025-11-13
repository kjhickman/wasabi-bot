using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.AI;

public sealed class GrokOptions
{
    public const string SectionName = "Grok";

    [Required]
    public string? ApiKey { get; set; }

    [Required]
    [Url]
    public string Endpoint { get; set; } = AIConstants.Endpoints.Grok;

    [Required]
    public string Model { get; set; } = AIConstants.Models.GrokFastNonReasoning;
}

