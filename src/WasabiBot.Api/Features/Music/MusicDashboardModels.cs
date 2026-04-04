namespace WasabiBot.Api.Features.Music;

internal sealed record SharedVoiceChannel(
    ulong GuildId,
    string GuildName,
    ulong VoiceChannelId,
    string VoiceChannelName);

internal sealed record MusicTrackSnapshot(
    string Title,
    string Author,
    string DurationText,
    TimeSpan? Duration,
    bool IsLive,
    bool IsRadio,
    string? ArtworkUrl,
    string? SourceUrl,
    string? SourceName);

internal sealed record MusicQueueItemSnapshot(int Position, MusicTrackSnapshot Track);

internal sealed record PlaybackProgressSnapshot(
    TimeSpan Position,
    string PositionText,
    double? Percent);

internal sealed record ActiveMusicSession(
    SharedVoiceChannel Channel,
    string PlaybackState,
    PlaybackProgressSnapshot? Progress,
    MusicTrackSnapshot? NowPlaying,
    IReadOnlyList<MusicQueueItemSnapshot> Queue);
