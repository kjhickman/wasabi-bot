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
    bool IsLive,
    bool IsRadio);

internal sealed record MusicQueueItemSnapshot(int Position, MusicTrackSnapshot Track);

internal sealed record ActiveMusicSession(
    SharedVoiceChannel Channel,
    MusicTrackSnapshot? NowPlaying,
    IReadOnlyList<MusicQueueItemSnapshot> Queue);
