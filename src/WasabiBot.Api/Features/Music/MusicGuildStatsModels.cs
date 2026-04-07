namespace WasabiBot.Api.Features.Music;

public sealed record GuildTopTrackSummary(
    string Title,
    string Artist,
    string SourceName,
    string SourceUrl,
    string ArtworkUrl,
    long PlayCount,
    DateTimeOffset LastPlayedAt);
