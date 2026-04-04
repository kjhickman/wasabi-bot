using System.Text.Json;
using Lavalink4NET.Tracks;
using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Persistence;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicFavoritesService(
    WasabiBotContext context,
    PlaybackService playbackService) : IMusicFavoritesService
{
    private readonly WasabiBotContext _context = context;
    private readonly PlaybackService _playbackService = playbackService;

    public async Task<MusicFavoritesSnapshot> ListAsync(long discordUserId, CancellationToken cancellationToken = default)
    {
        var favorites = await _context.MusicFavorites
            .Where(favorite => favorite.DiscordUserId == discordUserId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var songs = favorites
            .Where(favorite => favorite.Kind == MusicFavoriteKind.Song)
            .Select(MapFavorite)
            .ToArray();

        var radioStations = favorites
            .Where(favorite => favorite.Kind == MusicFavoriteKind.Radio)
            .Select(MapFavorite)
            .ToArray();

        return new MusicFavoritesSnapshot(songs, radioStations);
    }

    public async Task<MusicCommandResult> AddSongAsync(long discordUserId, LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        var snapshot = _playbackService.CreateTrackSnapshot(track);
        if (snapshot is null)
        {
            return new MusicCommandResult("Couldn't save that song as a favorite.", Ephemeral: true);
        }

        var externalId = !string.IsNullOrEmpty(snapshot.SourceUrl)
            ? snapshot.SourceUrl
            : $"{snapshot.SourceName}:{track.Identifier}";

        var exists = await _context.MusicFavorites.AnyAsync(
            favorite => favorite.DiscordUserId == discordUserId
                && favorite.Kind == MusicFavoriteKind.Song
                && favorite.ExternalId == externalId,
            cancellationToken);

        if (exists)
        {
            return new MusicCommandResult("That song is already in your favorites.", Ephemeral: true);
        }

        _context.MusicFavorites.Add(new MusicFavoriteEntity
        {
            DiscordUserId = discordUserId,
            Kind = MusicFavoriteKind.Song,
            ExternalId = externalId,
            Title = snapshot.Title,
            ArtistOrSubtitle = snapshot.Author,
            SourceName = snapshot.SourceName ?? string.Empty,
            SourceUrl = snapshot.SourceUrl ?? string.Empty,
            ArtworkUrl = snapshot.ArtworkUrl ?? string.Empty,
            MetadataJson = JsonSerializer.Serialize(new MusicFavoriteSongMetadata(
                track.Identifier,
                snapshot.SourceName ?? string.Empty,
                snapshot.SourceUrl ?? string.Empty,
                snapshot.ArtworkUrl ?? string.Empty,
                snapshot.DurationText)),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return new MusicCommandResult($"Saved **{snapshot.Title}** to your song favorites.");
    }

    public async Task<MusicCommandResult> AddRadioAsync(long discordUserId, RadioBrowserStation station, CancellationToken cancellationToken = default)
    {
        var exists = await _context.MusicFavorites.AnyAsync(
            favorite => favorite.DiscordUserId == discordUserId
                && favorite.Kind == MusicFavoriteKind.Radio
                && favorite.ExternalId == station.StationUuid,
            cancellationToken);

        if (exists)
        {
            return new MusicCommandResult("That radio station is already in your favorites.", Ephemeral: true);
        }

        _context.MusicFavorites.Add(new MusicFavoriteEntity
        {
            DiscordUserId = discordUserId,
            Kind = MusicFavoriteKind.Radio,
            ExternalId = station.StationUuid,
            Title = station.Name,
            ArtistOrSubtitle = station.Country,
            SourceName = "Radio Browser",
            SourceUrl = station.Homepage,
            ArtworkUrl = station.Favicon,
            MetadataJson = JsonSerializer.Serialize(new MusicFavoriteRadioMetadata(
                station.StationUuid,
                station.UrlResolved,
                station.Homepage,
                station.Favicon,
                station.Country,
                station.Tags)),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return new MusicCommandResult($"Saved **{station.Name}** to your radio favorites.");
    }

    public async Task<MusicCommandResult> RemoveAsync(long discordUserId, long favoriteId, CancellationToken cancellationToken = default)
    {
        var favorite = await _context.MusicFavorites.FirstOrDefaultAsync(
            item => item.Id == favoriteId && item.DiscordUserId == discordUserId,
            cancellationToken);

        if (favorite is null)
        {
            return new MusicCommandResult("That favorite no longer exists.", Ephemeral: true);
        }

        _context.MusicFavorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
        return new MusicCommandResult($"Removed **{favorite.Title}** from your favorites.");
    }

    private static MusicFavoriteSummary MapFavorite(MusicFavoriteEntity favorite)
    {
        return favorite.Kind switch
        {
            MusicFavoriteKind.Song => new MusicFavoriteSummary(
                favorite.Id,
                favorite.Kind,
                favorite.Title,
                favorite.ArtistOrSubtitle,
                favorite.SourceName,
                favorite.SourceUrl,
                favorite.ArtworkUrl,
                favorite.CreatedAt,
                Deserialize<MusicFavoriteSongMetadata>(favorite.MetadataJson),
                null),
            _ => new MusicFavoriteSummary(
                favorite.Id,
                favorite.Kind,
                favorite.Title,
                favorite.ArtistOrSubtitle,
                favorite.SourceName,
                favorite.SourceUrl,
                favorite.ArtworkUrl,
                favorite.CreatedAt,
                null,
                Deserialize<MusicFavoriteRadioMetadata>(favorite.MetadataJson))
        };
    }

    private static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json);
    }
}
