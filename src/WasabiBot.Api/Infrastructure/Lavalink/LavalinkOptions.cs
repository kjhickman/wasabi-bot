using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Infrastructure.Lavalink;

public sealed class LavalinkOptions
{
    public const string SectionName = "Lavalink";

    [Required]
    public string? BaseUrl { get; set; }

    [Required]
    public string? Password { get; set; }

    [Range(1, 3600)]
    public int ResumeTimeoutSeconds { get; set; } = 60;
}
