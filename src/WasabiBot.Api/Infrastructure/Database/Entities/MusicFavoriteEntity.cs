namespace WasabiBot.Api.Infrastructure.Database.Entities;

public sealed class MusicFavoriteEntity
{
    public long Id { get; set; }
    public long DiscordUserId { get; set; }
    public MusicFavoriteKind Kind { get; set; }
    public required string ExternalId { get; set; }
    public required string Title { get; set; }
    public string ArtistOrSubtitle { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public enum MusicFavoriteKind
{
    Song = 1,
    Radio = 2,
}
