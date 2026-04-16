using Lavalink4NET.Tracks;
using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Database.Entities;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicPlaybackStatsRecorder(
    IServiceScopeFactory serviceScopeFactory,
    RadioTrackMetadataStore radioTrackMetadataStore,
    ILogger<MusicPlaybackStatsRecorder> logger) : IMusicPlaybackStatsRecorder
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;
    private readonly ILogger<MusicPlaybackStatsRecorder> _logger = logger;

    public async Task RecordTrackStartedAsync(ulong guildId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        if (_radioTrackMetadataStore.TryGet(track, out _))
        {
            return;
        }

        var externalId = BuildExternalId(track);
        if (externalId.Length == 0)
        {
            return;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WasabiBotContext>();
            var now = DateTimeOffset.UtcNow;

            var play = await context.GuildTrackPlays.FirstOrDefaultAsync(
                item => item.GuildId == (long)guildId && item.ExternalId == externalId,
                cancellationToken);

            if (play is null)
            {
                context.GuildTrackPlays.Add(new GuildTrackPlayEntity
                {
                    GuildId = (long)guildId,
                    ExternalId = externalId,
                    Title = track.Title,
                    Artist = track.Author,
                    SourceName = track.SourceName ?? string.Empty,
                    SourceUrl = track.Uri?.ToString() ?? string.Empty,
                    ArtworkUrl = track.ArtworkUri?.ToString() ?? string.Empty,
                    PlayCount = 1,
                    FirstPlayedAt = now,
                    LastPlayedAt = now,
                });
            }
            else
            {
                play.Title = track.Title;
                play.Artist = track.Author;
                play.SourceName = track.SourceName ?? string.Empty;
                play.SourceUrl = track.Uri?.ToString() ?? string.Empty;
                play.ArtworkUrl = track.ArtworkUri?.ToString() ?? string.Empty;
                play.PlayCount += 1;
                play.LastPlayedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record music play stats for guild {GuildId} and track {TrackTitle}", guildId, track.Title);
        }
    }

    private static string BuildExternalId(LavalinkTrack track)
    {
        if (track.Uri is not null)
        {
            return track.Uri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(track.SourceName) && !string.IsNullOrWhiteSpace(track.Identifier))
        {
            return $"{track.SourceName}:{track.Identifier}";
        }

        return string.Empty;
    }
}
