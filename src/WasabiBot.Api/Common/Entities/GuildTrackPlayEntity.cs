namespace WasabiBot.Api.Persistence.Entities;

public sealed class GuildTrackPlayEntity
{
    public long Id { get; set; }
    public long GuildId { get; set; }
    public required string ExternalId { get; set; }
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public long PlayCount { get; set; }
    public DateTimeOffset FirstPlayedAt { get; set; }
    public DateTimeOffset LastPlayedAt { get; set; }
}
