namespace WasabiBot.Api.Features.Music;

public sealed record SharedVoiceChannel(
    ulong GuildId,
    string GuildName,
    ulong VoiceChannelId,
    string VoiceChannelName);

public sealed record UserVoiceChannel(
    ulong GuildId,
    string GuildName,
    ulong VoiceChannelId,
    string VoiceChannelName,
    bool BotIsConnectedInGuild,
    bool BotSharesChannel);

public sealed record MusicTrackSnapshot(
    string Title,
    string Author,
    string DurationText,
    TimeSpan? Duration,
    bool IsLive,
    bool IsRadio,
    string? ArtworkUrl,
    string? SourceUrl,
    string? SourceName);

public sealed record MusicQueueItemSnapshot(int Position, MusicTrackSnapshot Track);

public sealed record PlaybackProgressSnapshot(
    TimeSpan Position,
    string PositionText,
    double? Percent);

public sealed record ActiveMusicSession(
    SharedVoiceChannel Channel,
    string PlaybackState,
    PlaybackProgressSnapshot? Progress,
    MusicTrackSnapshot? NowPlaying,
    IReadOnlyList<MusicQueueItemSnapshot> Queue,
    UserVoiceChannel? UserChannel);
