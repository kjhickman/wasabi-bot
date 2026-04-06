namespace WasabiBot.Api.Features.Music;

internal sealed record GuildTopTrackSummary(
    string Title,
    string Artist,
    string SourceName,
    string SourceUrl,
    string ArtworkUrl,
    long PlayCount,
    DateTimeOffset LastPlayedAt);
