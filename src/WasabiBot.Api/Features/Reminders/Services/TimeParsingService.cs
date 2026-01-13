using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Abstractions;
using WasabiBot.Api.Infrastructure.AI;

namespace WasabiBot.Api.Features.Reminders.Services;

internal sealed class TimeParsingService : ITimeParsingService
{
    private static readonly string[] CentralTimeZoneIds = ["America/Chicago", "Central Standard Time"];
    private static readonly TimeZoneInfo CentralTimeZone = ResolveCentralTimeZone();

    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TimeParsingService> _logger;
    private readonly IChatClient _chatClient;
    private readonly Tracer _tracer;

    public TimeParsingService(TimeProvider timeProvider, ILogger<TimeParsingService> logger, IChatClientFactory chatClientFactory, Tracer tracer)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        _chatClient = chatClientFactory.GetChatClient(LlmPreset.LowLatency);
        _tracer = tracer;
    }

    public async Task<DateTimeOffset?> ParseTimeAsync(string timeInput)
    {
        if (string.IsNullOrWhiteSpace(timeInput))
        {
            throw new ArgumentException("A time input is required.", nameof(timeInput));
        }

        var nowUtc = _timeProvider.GetUtcNow();
        var currentCentralTime = TimeZoneInfo.ConvertTime(nowUtc, CentralTimeZone);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(currentCentralTime)),
            new(ChatRole.User, timeInput.Trim())
        };

        using var span = _tracer.StartActiveSpan("reminders.time.parse");

        try
        {
            var response = await _chatClient.GetResponseAsync(messages).ConfigureAwait(false);            
            var timestampText = response.Text?.Trim();

            if (string.IsNullOrWhiteSpace(timestampText))
            {
                throw new InvalidOperationException("LLM returned an empty response when parsing reminder time.");
            }

            if (!DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedUtc))
            {
                throw new FormatException($"LLM response '{timestampText}' was not a valid ISO 8601 UTC timestamp.");
            }

            return parsedUtc.ToUniversalTime();
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Failed to parse reminder time from input '{Input}'", timeInput);
            throw;
        }
    }

    private static string BuildSystemPrompt(DateTimeOffset currentCentralTime)
    {
        var isoCentral = currentCentralTime.ToString("O", CultureInfo.InvariantCulture);
        var currentTimeOfDay = currentCentralTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        var offset = currentCentralTime.Offset;
        var offsetAbs = offset.Duration().ToString("hh\\:mm", CultureInfo.InvariantCulture);
        var offsetHours = offset >= TimeSpan.Zero ? $"+{offsetAbs}" : $"-{offsetAbs}";
        var sampleLocal = currentCentralTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        var sampleUtc = currentCentralTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return $"""
You are a time-parsing assistant for a reminder bot. The current Central Time (America/Chicago) is {isoCentral} (UTC offset {offsetHours}).
Rules:
1. Interpret every request relative to Central Time.
2. If a user supplies a month/day that has already passed this year, schedule it for the next year instead.
3. If the user gives a relative day without a clock time (e.g., "tomorrow", "in 3 days"), preserve the current time of day ({currentTimeOfDay} Central) instead of switching to midnight.
4. Relative expressions ("in 45 minutes") should offset from the provided current time.
5. After you determine the correct Central reminder time, convert it to UTC by applying the Central-to-UTC offset. Example: Central {sampleLocal} becomes UTC {sampleUtc}.
6. Output only the final reminder time as an ISO 8601 UTC timestamp like 2025-08-08T14:00:00Z. No prose, no timezone abbreviations, no markdown.
7. The output must always end with 'Z' to indicate UTC.
""";
    }

    private static TimeZoneInfo ResolveCentralTimeZone()
    {
        foreach (var zoneId in CentralTimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // continue
            }
            catch (InvalidTimeZoneException)
            {
                // continue
            }
        }

        throw new InvalidOperationException("Unable to locate the Central Time zone.");
    }
}
