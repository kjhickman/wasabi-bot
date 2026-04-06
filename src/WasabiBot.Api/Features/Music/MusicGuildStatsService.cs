using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Persistence;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicGuildStatsService(WasabiBotContext context) : IMusicGuildStatsService
{
    private readonly WasabiBotContext _context = context;

    public async Task<IReadOnlyList<GuildTopTrackSummary>> GetTopTracksAsync(ulong guildId, int limit = 10, CancellationToken cancellationToken = default)
    {
        return await _context.GuildTrackPlays
            .Where(item => item.GuildId == (long)guildId)
            .OrderByDescending(item => item.PlayCount)
            .ThenByDescending(item => item.LastPlayedAt)
            .Take(limit)
            .Select(item => new GuildTopTrackSummary(
                item.Title,
                item.Artist,
                item.SourceName,
                item.SourceUrl,
                item.ArtworkUrl,
                item.PlayCount,
                item.LastPlayedAt))
            .ToArrayAsync(cancellationToken);
    }
}
