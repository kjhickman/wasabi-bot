using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Radio;

internal sealed class RadioService(
    HttpClient httpClient,
    IAudioService audioService,
    PlaybackService playbackService,
    RadioTrackMetadataStore radioTrackMetadataStore,
    ILogger<RadioService> logger,
    Tracer tracer) : IRadioService
{
    private const int StationSearchLimit = 10;
    private const int PlaybackAttemptLimit = 5;

    private readonly HttpClient _httpClient = httpClient;
    private readonly IAudioService _audioService = audioService;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly RadioTrackMetadataStore _radioTrackMetadataStore = radioTrackMetadataStore;
    private readonly ILogger<RadioService> _logger = logger;
    private readonly Tracer _tracer = tracer;

    public async Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string query, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("radio.play");
        AddContextAttributes(span, ctx);

        if (!ctx.GuildId.HasValue)
        {
            return PlaybackService.GuildOnly();
        }

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return new MusicCommandResult("Give me a radio station name or genre to search for.", Ephemeral: true);
        }

        var stations = await SearchStationsAsync(normalizedQuery, cancellationToken);
        if (stations.Count == 0)
        {
            return new MusicCommandResult("I couldn't find a playable radio station for that search.", Ephemeral: true);
        }

        var playerResult = await _playbackService.RetrievePlaybackPlayerAsync(ctx, cancellationToken);
        if (playerResult.Result is not null)
        {
            return playerResult.Result;
        }

        var player = playerResult.Player!;
        var attempts = 0;
        foreach (var station in stations)
        {
            if (attempts++ >= PlaybackAttemptLimit)
            {
                break;
            }

            var loadResult = await _audioService.Tracks.LoadTracksAsync(
                station.UrlResolved,
                new TrackLoadOptions(SearchBehavior: StrictSearchBehavior.Passthrough),
                cancellationToken: cancellationToken);

            if (!loadResult.HasMatches)
            {
                if (loadResult.Exception is { } stationException)
                {
                    span.RecordException(new InvalidOperationException(stationException.Message));
                    _logger.LogWarning(
                        "Lavalink failed to load radio stream {StationName} ({StationUrl}): {Exception}",
                        station.Name,
                        station.UrlResolved,
                        stationException.Message);
                }

                continue;
            }

            var track = loadResult.Track ?? loadResult.Tracks[0];
            _radioTrackMetadataStore.Set(track, station.Name);
            var position = await player.PlayAsync(track, enqueue: true, cancellationToken: cancellationToken);
            return PlaybackService.BuildQueuedDisplayResult(FormatStation(station), position);
        }

        return new MusicCommandResult(
            "I found radio stations for that search, but none of their streams were playable right now.",
            Ephemeral: true);
    }

    internal static IReadOnlyList<RadioBrowserStation> RankStations(string query, IEnumerable<RadioBrowserStation> stations)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return stations
            .Where(IsPlayableCandidate)
            .OrderByDescending(station => ScoreStation(station, normalizedQuery, tokens))
            .ThenByDescending(station => station.Votes)
            .ThenByDescending(station => station.ClickCount)
            .ThenByDescending(station => station.Bitrate)
            .ToArray();
    }

    public async Task<IReadOnlyList<RadioBrowserStation>> SearchStationsAsync(string query, CancellationToken cancellationToken = default)
    {
        var requestUri = $"stations/search?hidebroken=true&limit={StationSearchLimit}&order=votes&reverse=true&name={Uri.EscapeDataString(query)}";
        var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stations = await response.Content.ReadFromJsonAsync<List<RadioBrowserStation>>(cancellationToken: cancellationToken)
            ?? [];

        return RankStations(query, stations);
    }

    private static bool IsPlayableCandidate(RadioBrowserStation station)
    {
        return station.LastCheckOk == 1
            && !string.IsNullOrWhiteSpace(station.Name)
            && Uri.TryCreate(station.UrlResolved, UriKind.Absolute, out _);
    }

    private static int ScoreStation(RadioBrowserStation station, string normalizedQuery, string[] tokens)
    {
        var name = station.Name.ToLowerInvariant();
        var tags = station.Tags.ToLowerInvariant();
        var country = station.Country.ToLowerInvariant();

        var score = 0;
        if (name == normalizedQuery)
        {
            score += 200;
        }
        else if (name.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            score += 125;
        }
        else if (name.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 75;
        }

        foreach (var token in tokens)
        {
            if (name.Contains(token, StringComparison.Ordinal))
            {
                score += 25;
            }

            if (tags.Contains(token, StringComparison.Ordinal))
            {
                score += 10;
            }

            if (country.Contains(token, StringComparison.Ordinal))
            {
                score += 5;
            }
        }

        score += Math.Min(station.Votes, 500) / 10;
        score += Math.Min(station.ClickCount, 1000) / 50;
        score += Math.Min(station.Bitrate, 320) / 32;
        return score;
    }

    private static string FormatStation(RadioBrowserStation station)
    {
        return $"**{station.Name}** (`LIVE`)";
    }

    private static void AddContextAttributes(TelemetrySpan span, ICommandContext ctx)
    {
    }
}
