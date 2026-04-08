namespace WasabiBot.Api.Features.Music;

internal interface IMusicGuildStatsService
{
    Task<IReadOnlyList<GuildTopTrackSummary>> GetTopTracksAsync(ulong guildId, int limit = 10, CancellationToken cancellationToken = default);
}
