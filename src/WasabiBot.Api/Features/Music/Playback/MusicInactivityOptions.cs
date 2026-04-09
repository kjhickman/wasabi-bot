using System.ComponentModel.DataAnnotations;

namespace WasabiBot.Api.Features.Music;

public sealed class MusicInactivityOptions
{
    public const string SectionName = "MusicInactivity";

    [Range(1, 1440)]
    public int IdleTimeoutMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int PausedTimeoutMinutes { get; set; } = 60;
}
